import { Component, OnInit } from '@angular/core';
import { MatBottomSheetRef } from '@angular/material/bottom-sheet';
import {RoutesListActions} from 'src/app/models/routes-list-actions.enum';
@Component({
  selector: 'app-routes-list-bottomsheet',
  templateUrl: './routes-list-bottomsheet.component.html',
  styleUrls: ['./routes-list-bottomsheet.component.scss']
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
