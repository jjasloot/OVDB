import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';
import { MatButton } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';
import { StationVisitLevel } from 'src/app/models/stationView.model';

export interface StationVisitDialogData {
  name: string;
  level: StationVisitLevel | null;
}

/** What the user chose to do with an already-visited station. */
export type StationVisitDialogResult = 'entryExit' | 'stopped' | 'remove';

@Component({
  selector: 'app-station-visit-dialog',
  templateUrl: './station-visit-dialog.component.html',
  styleUrl: './station-visit-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatButton, TranslateModule],
})
export class StationVisitDialogComponent {
  private dialogRef = inject(MatDialogRef<StationVisitDialogComponent, StationVisitDialogResult>);
  data = inject<StationVisitDialogData>(MAT_DIALOG_DATA);

  readonly levels = StationVisitLevel;

  close(result: StationVisitDialogResult): void {
    this.dialogRef.close(result);
  }
}
