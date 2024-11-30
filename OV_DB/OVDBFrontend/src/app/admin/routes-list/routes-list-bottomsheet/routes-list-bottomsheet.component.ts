import { Component, OnInit } from '@angular/core';
import { MatBottomSheetRef } from '@angular/material/bottom-sheet';
import {RoutesListActions} from 'src/app/models/routes-list-actions.enum';
import { MatActionList, MatListItem } from '@angular/material/list';
import { MatIcon } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
@Component({
    selector: 'app-routes-list-bottomsheet',
    templateUrl: './routes-list-bottomsheet.component.html',
    styleUrls: ['./routes-list-bottomsheet.component.scss'],
    imports: [MatActionList, MatListItem, MatIcon, TranslateModule]
})
export class RoutesListBottomsheetComponent implements OnInit {

  constructor(
    public dialogRef: MatBottomSheetRef<RoutesListBottomsheetComponent>,
  ) {  }

  ngOnInit(): void {
  }

  view() {
    this.dialogRef.dismiss(RoutesListActions.View);
  }
  instances() {
    this.dialogRef.dismiss(RoutesListActions.Instances);
  }
  edit() {
    this.dialogRef.dismiss(RoutesListActions.Edit);
  }
}
