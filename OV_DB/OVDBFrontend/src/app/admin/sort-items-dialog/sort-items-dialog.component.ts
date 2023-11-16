import { Component, OnInit, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-sort-items-dialog',
  templateUrl: './sort-items-dialog.component.html',
  styleUrls: ['./sort-items-dialog.component.scss']
})
export class SortItemsDialogComponent implements OnInit {
  items: any;
  title: any;

  constructor(
    private translationService: TranslationService,
    public dialogRef: MatDialogRef<SortItemsDialogComponent>,
    @Inject(MAT_DIALOG_DATA) data) {
    if (!!data && data.list) {
      this.items = data.list;
      this.title = data.title;
    }
  }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }
  drop(event: CdkDragDrop<string[]>) {
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
  }
  ngOnInit(): void {
  }
  return() {
    this.dialogRef.close(this.items);
  }
  cancel() {
    this.dialogRef.close(false);
  }

}
