import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatOption } from '@angular/material/core';
import { MatSelect } from '@angular/material/select';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatIcon } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import { Map } from 'src/app/models/map.model';
import { HighlightTrip, NameCount, YearInReview } from 'src/app/models/year-in-review.model';

@Component({
  selector: 'app-year-in-review',
  templateUrl: './year-in-review.component.html',
  styleUrl: './year-in-review.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatCard,
    MatCardTitle,
    MatCardContent,
    MatFormField,
    MatLabel,
    MatSelect,
    MatOption,
    MatProgressSpinner,
    MatIcon,
    TranslateModule,
    BaseChartDirective,
  ],
})
export class YearInReviewComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private translateService = inject(TranslateService);

  maps = signal<Map[]>([]);
  years = signal<number[]>([]);
  selectedMap = signal<string | null>(null);
  selectedYear = signal<number | null>(null);
  review = signal<YearInReview | null>(null);
  loading = signal(false);

  private currentLanguage = toSignal(this.translationService.languageChanged);

  /** Percentage change in distance against the previous year, null when there is nothing to compare. */
  /**
   * Travel-day calendar: weeks as columns (Monday on top), one square per day. Levels 1–4 are
   * quartiles of your own travel days, so a quiet year still shows contrast.
   */
  calendar = computed(() => {
    const r = this.review();
    if (!r) {
      return null;
    }
    const daily = r.dailyDistanceKm ?? {};
    const values = Object.values(daily).filter((v) => v > 0).sort((a, b) => a - b);
    const quartile = (q: number) => values[Math.min(values.length - 1, Math.floor(q * values.length))] ?? 0;
    const cuts = [quartile(0.25), quartile(0.5), quartile(0.75)];
    const first = new Date(r.year, 0, 1);
    const start = new Date(first);
    start.setDate(first.getDate() - ((first.getDay() + 6) % 7)); // back to Monday
    const cells: { key: string; date: Date | null; km: number; level: number }[] = [];
    for (const d = new Date(start); d.getFullYear() <= r.year; d.setDate(d.getDate() + 1)) {
      const inYear = d.getFullYear() === r.year;
      const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
      const km = inYear ? daily[key] ?? 0 : 0;
      const level = km <= 0 ? 0 : km <= cuts[0] ? 1 : km <= cuts[1] ? 2 : km <= cuts[2] ? 3 : 4;
      cells.push({ key, date: inYear ? new Date(d) : null, km, level });
    }
    return cells;
  });

  distanceTrend = computed(() => {
    const review = this.review();
    if (!review || review.previousYearDistanceKm <= 0) {
      return null;
    }
    const change = ((review.distanceKm - review.previousYearDistanceKm) / review.previousYearDistanceKm) * 100;
    return Math.round(change * 10) / 10;
  });

  monthlyChart = computed<ChartConfiguration<'bar'>['data'] | null>(() => {
    const review = this.review();
    const language = this.currentLanguage() ?? this.translationService.language;
    if (!review || review.monthlyDistanceKm.every((value) => value === 0)) {
      return null;
    }
    const locale = language === 'nl' ? 'nl-NL' : 'en-GB';
    const labels = review.monthlyDistanceKm.map((_, index) =>
      new Date(review.year, index, 1).toLocaleDateString(locale, { month: 'short' })
    );
    return {
      labels,
      datasets: [
        {
          data: review.monthlyDistanceKm,
          backgroundColor: '#3f7d3f',
          label: this.translateService.instant('DISTANCE'),
        },
      ],
    };
  });

  monthlyChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true } },
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
    this.review.set(null);
    this.apiService.getYears(mapGuid).subscribe((years) => {
      const sorted = [...years].sort((a, b) => b - a);
      this.years.set(sorted);
      const currentYear = new Date().getFullYear();
      this.selectedYear.set(sorted.includes(currentYear) ? currentYear : (sorted[0] ?? currentYear));
      this.load();
    });
  }

  changeYear(year: number): void {
    this.selectedYear.set(year);
    this.load();
  }

  name(item: NameCount | HighlightTrip): string {
    return this.translationService.getNameForItem(item);
  }

  countryName(country: { name: string; nameNL: string }): string {
    return this.translationService.getNameForItem(country);
  }

  private load(): void {
    const map = this.selectedMap();
    if (!map) {
      return;
    }
    this.loading.set(true);
    this.apiService.getYearInReview(map, this.selectedYear()).subscribe({
      next: (review) => {
        this.review.set(review);
        this.loading.set(false);
      },
      error: () => {
        this.review.set(null);
        this.loading.set(false);
      },
    });
  }
}
