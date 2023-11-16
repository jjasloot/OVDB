import { Component, OnInit, Inject } from '@angular/core';
import { MatLegacyDialogRef as MatDialogRef, MAT_LEGACY_DIALOG_DATA as MAT_DIALOG_DATA } from '@angular/material/legacy-dialog';

@Component({
  selector: 'app-are-you-sure-dialog',
  templateUrl: './are-you-sure-dialog.component.html',
  styleUrls: ['./are-you-sure-dialog.component.scss']
})
export class AreYouSureDialogComponent implements OnInit {
  item: any;

  constructor(
    public dialogRef: MatDialogRef<AreYouSureDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data) {
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
