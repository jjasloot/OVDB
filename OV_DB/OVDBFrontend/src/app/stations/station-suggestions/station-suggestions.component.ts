import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from "@angular/core";
import { MatButton } from "@angular/material/button";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { DatePipe, DecimalPipe } from "@angular/common";
import { TranslateModule } from "@ngx-translate/core";
import { firstValueFrom } from "rxjs";
import { ApiService } from "src/app/services/api.service";
import { StationVisitLevel, TripSuggestions } from "src/app/models/stationView.model";

/**
 * Stations your recent trips pass that are not marked visited.
 *
 * This is the only screen that turns inference into visits, so it does it one tick at a time and
 * never in bulk: proximity is only about 66% precise, and a measured 14% of unvisited stations sit
 * within 300 m of a route that has been ridden. Nothing here is pre-ticked, and leaving a row alone
 * does nothing at all.
 *
 * It is a list to come back to rather than a prompt after importing, because Träwelling check-ins
 * arrive in the background — there is no import moment to interrupt.
 */
@Component({
  selector: "app-station-suggestions",
  templateUrl: "./station-suggestions.component.html",
  styleUrl: "./station-suggestions.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButton, MatProgressSpinner, DatePipe, DecimalPipe, TranslateModule],
})
export class StationSuggestionsComponent implements OnInit {
  private apiService = inject(ApiService);

  trips = signal<TripSuggestions[]>([]);
  loading = signal(true);
  /** Stations resolved this session, so a row can disappear without refetching everything. */
  busy = signal<Set<number>>(new Set());
  /** Trips the user has asked to see in full. */
  private expanded = signal<Set<number>>(new Set());

  readonly levels = StationVisitLevel;

  /**
   * A long journey should not open with a wall of proposals — one measured trip offers 33. Endpoint
   * matches always show, since they are the ones worth acting on; the weak proximity tail collapses
   * behind a count.
   */
  private static readonly COLLAPSE_AFTER = 5;

  visibleStations(trip: TripSuggestions) {
    if (this.expanded().has(trip.routeInstanceId)) {
      return trip.stations;
    }
    return trip.stations.slice(0, StationSuggestionsComponent.COLLAPSE_AFTER);
  }

  hiddenCount(trip: TripSuggestions): number {
    return this.expanded().has(trip.routeInstanceId)
      ? 0
      : Math.max(0, trip.stations.length - StationSuggestionsComponent.COLLAPSE_AFTER);
  }

  expand(routeInstanceId: number): void {
    this.expanded.update((current) => new Set(current).add(routeInstanceId));
  }

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.trips.set(await firstValueFrom(this.apiService.getStationSuggestions()));
    } catch {
      this.trips.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  isBusy(stationId: number): boolean {
    return this.busy().has(stationId);
  }

  async mark(trip: TripSuggestions, stationId: number, level: StationVisitLevel): Promise<void> {
    this.setBusy(stationId, true);
    try {
      await firstValueFrom(this.apiService.markSuggestedStation(stationId, trip.routeInstanceId, level));
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

  /** A station can be suggested by several trips, so it goes from all of them at once. */
  private remove(stationId: number): void {
    this.trips.update((trips) =>
      trips
        .map((trip) => ({ ...trip, stations: trip.stations.filter((s) => s.stationId !== stationId) }))
        .filter((trip) => trip.stations.length > 0)
    );
    this.setBusy(stationId, false);
  }
}
