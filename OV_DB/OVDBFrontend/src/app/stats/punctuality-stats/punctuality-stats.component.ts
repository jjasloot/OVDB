import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatOption } from '@angular/material/core';
import { MatSelect } from '@angular/material/select';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import { Map } from 'src/app/models/map.model';
import { DelayedTrip, PunctualityStats } from 'src/app/models/punctuality.model';

// Green through red, matching the order the backend returns the buckets in.
const BUCKET_COLOURS: Record<string, string> = {
  EARLY: '#1b5e20',
  ONTIME: '#66bb6a',
  D5_15: '#fdd835',
  D15_30: '#fb8c00',
  D30_60: '#e53935',
  D60PLUS: '#8e0000',
};

@Component({
  selector: 'app-punctuality-stats',
  templateUrl: './punctuality-stats.component.html',
  styleUrl: './punctuality-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatCard,
    MatCardTitle,
    MatCardContent,
    MatFormField,
    MatLabel,
    MatSelect,
    MatOption,
    MatProgressSpinner,
    TranslateModule,
    BaseChartDirective,
  ],
})
export class PunctualityStatsComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private translateService = inject(TranslateService);

  maps = signal<Map[]>([]);
  years = signal<number[]>([]);
  selectedMap = signal<string | null>(null);
  selectedYear = signal<number | null>(null);
  stats = signal<PunctualityStats | null>(null);
  loading = signal(false);

  // Read inside the computed below so chart labels re-translate on a language switch.
  private currentLanguage = toSignal(this.translationService.languageChanged);

  hasArrivalData = computed(() => (this.stats()?.tripsWithArrivalData ?? 0) > 0);

  chartData = computed<ChartConfiguration<'bar'>['data'] | null>(() => {
    const stats = this.stats();
    this.currentLanguage();
    if (!stats || stats.arrivalDelayDistribution.length === 0) {
      return null;
    }
    return {
      labels: stats.arrivalDelayDistribution.map((bucket) =>
        this.translateService.instant('STATS.PUNCTUALITY.BUCKET.' + bucket.key)
      ),
      datasets: [
        {
          data: stats.arrivalDelayDistribution.map((bucket) => bucket.count),
          backgroundColor: stats.arrivalDelayDistribution.map(
            (bucket) => BUCKET_COLOURS[bucket.key] ?? '#888888'
          ),
          label: this.translateService.instant('STATS.PUNCTUALITY.TRIPS'),
        },
      ],
    };
  });

  chartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
  };

  ngOnInit(): void {
    this.apiService.getMaps().subscribe((maps) => {
      this.maps.set(maps);
      if (maps.length > 0) {
        const defaultMap = maps.find((m) => m.default) ?? maps[0];
        this.changeMap(defaultMap.mapGuid);
      }
    });
  }

  changeMap(mapGuid: string): void {
    this.selectedMap.set(mapGuid);
    this.stats.set(null);
    this.apiService.getYears(mapGuid).subscribe((years) => {
      const sorted = [...years].sort((a, b) => b - a);
      this.years.set(sorted);
      const currentYear = new Date().getFullYear();
      this.selectedYear.set(sorted.includes(currentYear) ? currentYear : (sorted[0] ?? null));
      this.load();
    });
  }

  changeYear(year: number): void {
    this.selectedYear.set(year === 0 ? null : year);
    this.load();
  }

  tripName(trip: DelayedTrip): string {
    return this.translationService.getNameForItem(trip);
  }

  private load(): void {
    const map = this.selectedMap();
    if (!map) {
      return;
    }
    this.loading.set(true);
    this.apiService.getPunctualityStats(map, this.selectedYear()).subscribe({
      next: (stats) => {
        this.stats.set(stats);
        this.loading.set(false);
      },
      error: () => {
        this.stats.set(null);
        this.loading.set(false);
      },
    });
  }
}
