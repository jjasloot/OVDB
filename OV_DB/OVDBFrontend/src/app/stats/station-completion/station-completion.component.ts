import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import { RegionStat } from 'src/app/models/region.model';
import { MissingStation } from 'src/app/models/missing-station.model';

interface CompletionRow {
  id: number;
  name: string;
  nameNL: string;
  flagEmoji: string | null;
  visited: number;
  /** Of the visited ones, how many you got on or off at. A subset of visited. */
  entryExit: number;
  total: number;
  percentage: number;
  entryExitPercentage: number;
  remaining: number;
}

@Component({
  selector: 'app-station-completion',
  templateUrl: './station-completion.component.html',
  styleUrl: './station-completion.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCard,
    MatCardTitle,
    MatCardContent,
    MatExpansionModule,

    MatProgressSpinner,
    TranslateModule,
  ],
})
export class StationCompletionComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);

  loading = signal(true);
  private regions = signal<RegionStat[]>([]);
  missingStations = signal<Record<number, MissingStation[]>>({});
  loadingMissing = signal<Record<number, boolean>>({});

  /** Every region in the tree flattened, since completion is per region at any depth. */
  private allRows = computed<CompletionRow[]>(() => {
    const rows: CompletionRow[] = [];
    const walk = (regions: RegionStat[]) => {
      for (const region of regions) {
        if (region.totalStations > 0) {
          rows.push({
            id: region.id,
            name: region.name,
            nameNL: region.nameNL,
            flagEmoji: region.flagEmoji,
            visited: region.visitedStations,
            entryExit: region.entryExitStations,
            total: region.totalStations,
            percentage: (region.visitedStations / region.totalStations) * 100,
            entryExitPercentage: (region.entryExitStations / region.totalStations) * 100,
            remaining: region.totalStations - region.visitedStations,
          });
        }
        if (region.children?.length) {
          walk(region.children);
        }
      }
    };
    walk(this.regions());
    return rows;
  });

  /** Started but not finished, closest to the finish line first - the actual gap finder. */
  inProgress = computed(() =>
    this.allRows()
      .filter((row) => row.visited > 0 && row.remaining > 0)
      .sort((a, b) => b.percentage - a.percentage || a.remaining - b.remaining)
  );

  completed = computed(() => this.allRows().filter((row) => row.remaining === 0));

  untouched = computed(() => this.allRows().filter((row) => row.visited === 0));

  totalVisited = computed(() => this.allRows().reduce((sum, row) => sum + row.visited, 0));

  totalEntryExit = computed(() => this.allRows().reduce((sum, row) => sum + row.entryExit, 0));

  ngOnInit(): void {
    this.apiService.getRegionStats().subscribe({
      next: (stats) => {
        this.regions.set(stats);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(row: CompletionRow): string {
    return this.translationService.getNameForItem(row);
  }

  /** Missing stations are fetched only when a region is actually opened. */
  loadMissing(regionId: number): void {
    if (this.missingStations()[regionId] || this.loadingMissing()[regionId]) {
      return;
    }
    this.loadingMissing.update((state) => ({ ...state, [regionId]: true }));
    this.apiService.getMissingStations(regionId).subscribe({
      next: (stations) => {
        this.missingStations.update((state) => ({ ...state, [regionId]: stations }));
        this.loadingMissing.update((state) => ({ ...state, [regionId]: false }));
      },
      error: () => this.loadingMissing.update((state) => ({ ...state, [regionId]: false })),
    });
  }
}
