import { Component, OnInit, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { CdkDragDrop, moveItemInArray, CdkDropList, CdkDrag } from '@angular/cdk/drag-drop';
import { TranslationService } from 'src/app/services/translation.service';
import { CdkScrollable } from '@angular/cdk/scrolling';
import { MatButton } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';

@Component({
    selector: 'app-sort-items-dialog',
    templateUrl: './sort-items-dialog.component.html',
    styleUrls: ['./sort-items-dialog.component.scss'],
    imports: [MatDialogTitle, CdkScrollable, MatDialogContent, CdkDropList, CdkDrag, MatDialogActions, MatButton, TranslateModule]
})
export class SortItemsDialogComponent implements OnInit {
  private translationService = inject(TranslationService);
  dialogRef = inject<MatDialogRef<SortItemsDialogComponent>>(MatDialogRef);

  items: any;
  title: any;

  constructor() {
    const data = inject(MAT_DIALOG_DATA);

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
