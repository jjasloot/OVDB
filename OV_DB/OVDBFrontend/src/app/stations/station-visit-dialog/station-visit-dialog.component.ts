import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatButton } from '@angular/material/button';
import { DatePipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { StationVisitLevel } from 'src/app/models/stationView.model';

export interface StationVisitDialogData {
  name: string;
  level: StationVisitLevel | null;
  firstStoppedDate: string | null;
  firstEntryExitDate: string | null;
}

/** What the user chose to do with an already-visited station. */
export type StationVisitDialogResult = 'entryExit' | 'stopped' | 'remove' | 'dates';

@Component({
  selector: 'app-station-visit-dialog',
  templateUrl: './station-visit-dialog.component.html',
  styleUrl: './station-visit-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatButton, DatePipe, TranslateModule],
})
export class StationVisitDialogComponent {
  private dialogRef = inject(MatDialogRef<StationVisitDialogComponent, StationVisitDialogResult>);
  data = inject<StationVisitDialogData>(MAT_DIALOG_DATA);

  readonly levels = StationVisitLevel;

  /**
   * The date worth showing is the one for the level the visit is at. Undated is the ordinary case
   * until the backfill has been through, and saying so is better than showing nothing.
   */
  get date(): string | null {
    return this.data.level === StationVisitLevel.EntryExit
      ? this.data.firstEntryExitDate
      : this.data.firstStoppedDate;
  }

  close(result: StationVisitDialogResult): void {
    this.dialogRef.close(result);
  }
}
