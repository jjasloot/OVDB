import { ChangeDetectionStrategy, Component, inject, signal } from "@angular/core";
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from "@angular/material/dialog";
import { MatButton } from "@angular/material/button";
import { DecimalPipe } from "@angular/common";
import { TranslateModule } from "@ngx-translate/core";
import { firstValueFrom } from "rxjs";
import { ApiService } from "src/app/services/api.service";
import { StationSuggestion, StationVisitLevel } from "src/app/models/stationView.model";

export interface StationSuggestionsDialogData {
  /** What the suggestions came from, shown so the user knows what they are answering about. */
  tripName: string;
  /**
   * The trip that supplies the date, when there is one. Träwelling imports have it; a freshly
   * imported OSM route does not, and marking then leaves an undated visit for the backfill.
   */
  routeInstanceId: number | null;
  stations: StationSuggestion[];
}

/**
 * Stations an import says the train called at, that are not marked visited.
 *
 * Shown once, at import, because that is the only moment the operator's calling pattern is on hand
 * — Träwelling stopovers or OSM stop members. Route geometry is deliberately not used here: a line
 * passing a station looks identical whether or not the train stopped, so it cannot support a
 * suggestion.
 *
 * Nothing is pre-ticked and there is no "accept all". Each station is marked, dismissed, or left
 * alone, and leaving it alone does nothing.
 */
@Component({
  selector: "app-station-suggestions",
  templateUrl: "./station-suggestions.component.html",
  styleUrl: "./station-suggestions.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatButton,
    DecimalPipe,
    TranslateModule,
  ],
})
export class StationSuggestionsComponent {
  private apiService = inject(ApiService);
  private dialogRef = inject(MatDialogRef<StationSuggestionsComponent>);
  data = inject<StationSuggestionsDialogData>(MAT_DIALOG_DATA);

  remaining = signal<StationSuggestion[]>(this.data.stations);
  busy = signal<Set<number>>(new Set());
  private expanded = signal(false);

  readonly levels = StationVisitLevel;

  /** A long journey should not open with a wall of proposals; one measured trip offers 33. */
  private static readonly COLLAPSE_AFTER = 8;

  visible() {
    return this.expanded()
      ? this.remaining()
      : this.remaining().slice(0, StationSuggestionsComponent.COLLAPSE_AFTER);
  }

  hiddenCount(): number {
    return this.expanded()
      ? 0
      : Math.max(0, this.remaining().length - StationSuggestionsComponent.COLLAPSE_AFTER);
  }

  expand(): void {
    this.expanded.set(true);
  }

  isBusy(stationId: number): boolean {
    return this.busy().has(stationId);
  }

  async mark(stationId: number, level: StationVisitLevel): Promise<void> {
    this.setBusy(stationId, true);
    try {
      // With a trip the visit is dated from it; without one it stays undated on purpose rather
      // than claiming today, which nothing here knows. Both go through the same endpoint so the
      // visit is recorded as import-suggested either way.
      await firstValueFrom(
        this.apiService.markSuggestedStation(stationId, this.data.routeInstanceId, level)
      );
      this.remove(stationId);
    } catch {
      this.setBusy(stationId, false);
    }
  }

  /** Says nothing about whether the station was visited — only that it should stop asking. */
  async dismiss(stationId: number): Promise<void> {
    this.setBusy(stationId, true);
    try {
      await firstValueFrom(this.apiService.dismissStationSuggestion(stationId));
      this.remove(stationId);
    } catch {
      this.setBusy(stationId, false);
    }
  }

  private setBusy(stationId: number, busy: boolean): void {
    this.busy.update((current) => {
      const next = new Set(current);
      if (busy) {
        next.add(stationId);
      } else {
        next.delete(stationId);
      }
      return next;
    });
  }

  private remove(stationId: number): void {
    this.remaining.update((stations) => stations.filter((s) => s.stationId !== stationId));
    this.setBusy(stationId, false);
    if (this.remaining().length === 0) {
      this.dialogRef.close();
    }
  }
}
