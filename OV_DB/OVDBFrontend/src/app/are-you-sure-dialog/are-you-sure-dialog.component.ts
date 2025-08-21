import { Component, OnInit, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { CdkScrollable } from '@angular/cdk/scrolling';
import { MatButton } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';

@Component({
    selector: 'app-are-you-sure-dialog',
    templateUrl: './are-you-sure-dialog.component.html',
    styleUrls: ['./are-you-sure-dialog.component.scss'],
    imports: [MatDialogTitle, CdkScrollable, MatDialogContent, MatDialogActions, MatButton, TranslateModule]
})
export class AreYouSureDialogComponent implements OnInit {
  dialogRef = inject<MatDialogRef<AreYouSureDialogComponent>>(MatDialogRef);
  data = inject(MAT_DIALOG_DATA);

  item: any;

  constructor() {
    const data = this.data;

    this.item = data.item;
  }

  ngOnInit() {
  }
  yes() {
    this.dialogRef.close(true);
  }

  no() {
    this.dialogRef.close(false);
  }

}
