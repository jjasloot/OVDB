import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatDatepicker, MatDatepickerInput, MatDatepickerToggle } from '@angular/material/datepicker';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatIcon } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import moment, { Moment } from 'moment';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import {
  StationVisitDates,
  StationVisitLevel,
  TripCandidateGroup,
} from 'src/app/models/stationView.model';

export interface StationVisitDateDialogData {
  stationId: number;
  name: string;
  level: StationVisitLevel | null;
  firstStoppedDate: string | null;
  firstStoppedRouteInstanceId: number | null;
  firstEntryExitDate: string | null;
  firstEntryExitRouteInstanceId: number | null;
}

/** Which of the two dates a candidate is being assigned to. */
type Target = 'stopped' | 'entryExit';

@Component({
  selector: 'app-station-visit-date-dialog',
  templateUrl: './station-visit-date-dialog.component.html',
  styleUrl: './station-visit-date-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatButton,
    MatIconButton,
    MatIcon,
    MatFormField,
    MatLabel,
    MatSuffix,
    MatInput,
    MatDatepicker,
    MatDatepickerInput,
    MatDatepickerToggle,
    MatProgressSpinner,
    FormsModule,
    DatePipe,
    DecimalPipe,
    TranslateModule,
  ],
})
export class StationVisitDateDialogComponent {
  private dialogRef = inject(MatDialogRef<StationVisitDateDialogComponent, StationVisitDates>);
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  data = inject<StationVisitDateDialogData>(MAT_DIALOG_DATA);

  readonly levels = StationVisitLevel;

  stoppedDate = signal<Moment | null>(this.data.firstStoppedDate ? moment(this.data.firstStoppedDate) : null);
  entryExitDate = signal<Moment | null>(this.data.firstEntryExitDate ? moment(this.data.firstEntryExitDate) : null);
  stoppedTrip = signal<number | null>(this.data.firstStoppedRouteInstanceId);
  entryExitTrip = signal<number | null>(this.data.firstEntryExitRouteInstanceId);

  candidates = signal<TripCandidateGroup[]>([]);
  loading = signal(true);
  expanded = signal<number | null>(null);

  /**
   * Alighting implies stopping, so the server pulls the stopped date back to meet an earlier
   * entry/exit date. Saying so up front beats surprising the user with it afterwards.
   */
  willPullStoppedBack = computed(() => {
    const stopped = this.stoppedDate();
    const entryExit = this.entryExitDate();
    return !!entryExit && (!stopped || entryExit.isBefore(stopped, 'day'));
  });

  constructor() {
    void this.loadCandidates();
  }

  private async loadCandidates(): Promise<void> {
    try {
      this.candidates.set(await firstValueFrom(this.apiService.getStationVisitCandidates(this.data.stationId)));
    } catch {
      this.candidates.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  toggle(routeId: number): void {
    this.expanded.update((current) => (current === routeId ? null : routeId));
  }

  /**
   * Which of the two choices this candidate suggests. Starting or ending at the station is the only
   * evidence of standing on the platform, so those lead with got-on/off; everything else leads with
   * the weaker claim.
   */
  primaryFor(group: TripCandidateGroup): 'stopped' | 'entryExit' {
    return group.isEndpoint ? 'entryExit' : 'stopped';
  }

  /** The user's own name for the route type, in their language. */
  typeName(group: TripCandidateGroup): string {
    return this.translationService.getNameForItem({
      name: group.routeTypeName,
      nameNL: group.routeTypeNameNL,
    });
  }

  /** Assigning a trip sets the date too — the server takes it from the trip, so they cannot differ. */
  choose(target: Target, routeInstanceId: number, date: string): void {
    if (target === 'entryExit') {
      this.entryExitDate.set(moment(date));
      this.entryExitTrip.set(routeInstanceId);
      return;
    }
    this.stoppedDate.set(moment(date));
    this.stoppedTrip.set(routeInstanceId);
  }

  /** Typing a date by hand means it is no longer that trip's date, so the link goes. */
  setDate(target: Target, value: Moment | null): void {
    if (target === 'entryExit') {
      this.entryExitDate.set(value);
      this.entryExitTrip.set(null);
      return;
    }
    this.stoppedDate.set(value);
    this.stoppedTrip.set(null);
  }

  clear(target: Target): void {
    this.setDate(target, null);
  }

  save(): void {
    this.dialogRef.close({
      firstStoppedDate: format(this.stoppedDate()),
      firstStoppedRouteInstanceId: this.stoppedTrip(),
      // Sent whatever the visit's current level is: filling this in is what raises it to got-on/off,
      // and leaving it blank is what takes that back down to merely stopped.
      firstEntryExitDate: format(this.entryExitDate()),
      firstEntryExitRouteInstanceId: this.entryExitTrip(),
    });
  }
}

/** The datepicker deals in moments; the API deals in plain dates with no timezone to misread. */
function format(value: Moment | null): string | null {
  return value ? moment(value).format('YYYY-MM-DD') : null;
}
