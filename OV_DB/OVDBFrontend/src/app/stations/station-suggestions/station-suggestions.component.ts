import { ChangeDetectionStrategy, Component, inject, signal } from "@angular/core";
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from "@angular/material/dialog";
import { MatButton, MatIconButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import { MatFormField } from "@angular/material/form-field";
import { MatSelect } from "@angular/material/select";
import { MatOption } from "@angular/material/core";
import { FormsModule } from "@angular/forms";
import { TranslateModule } from "@ngx-translate/core";
import { firstValueFrom } from "rxjs";
import { ApiService } from "src/app/services/api.service";
import { StationSuggestion, StationVisitLevel } from "src/app/models/stationView.model";

export interface StationSuggestionsDialogData {
  /** What the suggestions came from, shown so the user knows what they are answering about. */
  tripName: string;
  /**
   * The trip that supplies the date, when there is one. Träwelling imports have it, and so does a
   * saved OSM route once it has a trip; without one, marking leaves an undated visit.
   */
  routeInstanceId: number | null;
  stations: StationSuggestion[];
}

/** What happened at a station. Started and finished both mean you were on the platform. */
type StopRole = "stopped" | "started" | "finished";

interface Row {
  station: StationSuggestion;
  role: StopRole;
  state: "open" | "busy" | "done";
}

/**
 * Stations an import says the train called at, that are not marked visited.
 *
 * Shown once, at import, because that is the only moment the operator's calling pattern is on hand —
 * Träwelling stopovers or OSM stop members. Route geometry is deliberately not used: a line passing
 * a station looks identical whether or not the train stopped, so it cannot support a suggestion.
 *
 * Rows keep the order the relation lists them, so the list reads like the journey. Each row is its
 * own decision — a role, then confirm or deny — and leaving one alone does nothing at all.
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
    MatDialogClose,
    MatButton,
    MatIconButton,
    MatIcon,
    MatFormField,
    MatSelect,
    MatOption,
    FormsModule,
    TranslateModule,
  ],
})
export class StationSuggestionsComponent {
  private apiService = inject(ApiService);
  private dialogRef = inject(MatDialogRef<StationSuggestionsComponent>);
  data = inject<StationSuggestionsDialogData>(MAT_DIALOG_DATA);

  readonly roles: StopRole[] = ["stopped", "started", "finished"];

  /**
   * Where the user boarded and got off default to started and finished — that is what being at the
   * end of a ridden section means — and everything between defaults to merely stopped, the weaker
   * claim. So a fast confirm-through never invents an alighting that did not happen.
   *
   * The flag has to come from the server, which knows the journey: position in this list cannot
   * stand in for it, because the ends of a journey are usually the stations the user has already
   * marked and so are missing here. Trusting position offered "boarded here" for whichever station
   * happened to survive the filter.
   */
  rows = signal<Row[]>(
    this.data.stations.map((station, index) => ({
      station,
      role:
        this.data.stations.length > 1 && station.isEndpoint
          ? index === 0
            ? "started"
            : index === this.data.stations.length - 1
              ? "finished"
              : "stopped"
          : "stopped",
      state: "open" as const,
    }))
  );

  private static readonly COLLAPSE_AFTER = 8;
  private expanded = signal(false);

  visible() {
    const open = this.rows().filter((r) => r.state !== "done");
    return this.expanded() ? open : open.slice(0, StationSuggestionsComponent.COLLAPSE_AFTER);
  }

  hiddenCount(): number {
    const open = this.rows().filter((r) => r.state !== "done").length;
    return this.expanded() ? 0 : Math.max(0, open - StationSuggestionsComponent.COLLAPSE_AFTER);
  }

  expand(): void {
    this.expanded.set(true);
  }

  roleLabel(role: StopRole): string {
    return `STATIONS.SUGGESTIONS.ROLE_${role.toUpperCase()}`;
  }

  setRole(stationId: number, role: StopRole): void {
    this.rows.update((rows) =>
      rows.map((row) => (row.station.stationId === stationId ? { ...row, role } : row))
    );
  }

  /** Confirms one row at the role chosen for it. */
  async confirm(row: Row): Promise<void> {
    this.setState(row.station.stationId, "busy");
    // Started and finished both mean you got on or off; only "stopped" is the weaker claim. The
    // model keeps two levels, so the third option is a clearer question, not a third stored state.
    const level = row.role === "stopped" ? StationVisitLevel.Stopped : StationVisitLevel.EntryExit;
    try {
      await firstValueFrom(
        this.apiService.markSuggestedStation(row.station.stationId, this.data.routeInstanceId, level)
      );
      this.setState(row.station.stationId, "done");
    } catch {
      this.setState(row.station.stationId, "open");
    }
  }

  /** Says nothing about whether the station was visited — only that it should stop asking. */
  async deny(row: Row): Promise<void> {
    this.setState(row.station.stationId, "busy");
    try {
      await firstValueFrom(this.apiService.dismissStationSuggestion(row.station.stationId));
      this.setState(row.station.stationId, "done");
    } catch {
      this.setState(row.station.stationId, "open");
    }
  }

  private setState(stationId: number, state: Row["state"]): void {
    this.rows.update((rows) =>
      rows.map((row) => (row.station.stationId === stationId ? { ...row, state } : row))
    );
    if (this.rows().every((row) => row.state === "done")) {
      this.dialogRef.close();
    }
  }
}
