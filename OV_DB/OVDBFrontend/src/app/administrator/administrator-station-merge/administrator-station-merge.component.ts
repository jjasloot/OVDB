import { DecimalPipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { MatButton, MatIconButton } from "@angular/material/button";
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from "@angular/material/card";
import { MatIcon } from "@angular/material/icon";
import { MatOption } from "@angular/material/core";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatSelect } from "@angular/material/select";
import { MatTooltip } from "@angular/material/tooltip";
import { StationMergeCountry, StationNearbyPair } from "src/app/models/stationMerge.model";
import { ApiService } from "src/app/services/api.service";

@Component({
  selector: "app-administrator-station-merge",
  templateUrl: "./administrator-station-merge.component.html",
  styleUrls: ["./administrator-station-merge.component.scss"],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButton,
    MatIconButton,
    MatCard,
    MatCardContent,
    MatCardHeader,
    MatCardTitle,
    MatIcon,
    MatOption,
    MatProgressSpinner,
    MatSelect,
    MatTooltip,
    DecimalPipe,
  ],
})
export class AdministratorStationMergeComponent implements OnInit {
  protected readonly Math = Math;
  private apiService = inject(ApiService);
  private destroyRef = inject(DestroyRef);

  countries = signal<StationMergeCountry[]>([]);
  selectedCountryId = signal<number | null>(null);
  pairs = signal<StationNearbyPair[]>([]);
  totalPairs = signal<number>(0);
  currentPage = signal<number>(0);
  pageSize = 10;
  loading = signal<boolean>(false);
  actionInProgress = signal<boolean>(false);

  ngOnInit(): void {
    this.apiService
      .getStationMergeCountries()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((data) => {
        this.countries.set(data);
      });
  }

  onCountryChange(countryId: number): void {
    this.selectedCountryId.set(countryId);
    this.currentPage.set(0);
    this.loadPairs();
  }

  loadPairs(): void {
    const countryId = this.selectedCountryId();
    if (!countryId) return;
    this.loading.set(true);
    this.apiService
      .getStationMergePairs(countryId, this.currentPage(), this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.pairs.set(data.pairs);
          this.totalPairs.set(data.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  merge(keepId: number, deleteId: number): void {
    this.actionInProgress.set(true);
    this.apiService
      .mergeStations(keepId, deleteId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.refreshAfterAction();
        },
        error: () => this.actionInProgress.set(false),
      });
  }

  skip(pair: StationNearbyPair): void {
    this.actionInProgress.set(true);
    this.apiService
      .skipStationPair(pair.station1Id, pair.station2Id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.refreshAfterAction();
        },
        error: () => this.actionInProgress.set(false),
      });
  }

  private refreshAfterAction(): void {
    // After an action the total changes; stay on current page (or go back if empty)
    const countryId = this.selectedCountryId();
    if (!countryId) return;
    this.loading.set(true);
    this.apiService
      .getStationMergePairs(countryId, this.currentPage(), this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          // If page is now empty and not the first page, go back one page
          if (data.pairs.length === 0 && this.currentPage() > 0) {
            this.currentPage.set(this.currentPage() - 1);
            this.loadPairs();
          } else {
            this.pairs.set(data.pairs);
            this.totalPairs.set(data.total);
            this.loading.set(false);
          }
        },
        error: () => this.loading.set(false),
      });
  }

  nextPage(): void {
    this.currentPage.set(this.currentPage() + 1);
    this.loadPairs();
  }

  prevPage(): void {
    if (this.currentPage() > 0) {
      this.currentPage.set(this.currentPage() - 1);
      this.loadPairs();
    }
  }

  get hasPrevPage(): boolean {
    return this.currentPage() > 0;
  }

  get hasNextPage(): boolean {
    return (this.currentPage() + 1) * this.pageSize < this.totalPairs();
  }

  openInOsm(lat: number, lon: number): string {
    return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lon}&zoom=17`;
  }
}
