import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { DataUpdateService } from 'src/app/services/data-update.service';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { AreYouSureDialogComponent } from 'src/app/are-you-sure-dialog/are-you-sure-dialog.component';
import { MatBottomSheetRef, MAT_BOTTOM_SHEET_DATA } from '@angular/material/bottom-sheet';
import { Map } from 'src/app/models/map.model';
import { MapListActions } from 'src/app/models/maps-list-actions.enum';
import { MatActionList, MatListItem } from '@angular/material/list';
import { MatIcon } from '@angular/material/icon';
import { CdkCopyToClipboard } from '@angular/cdk/clipboard';
@Component({
    selector: 'app-maps-list-bottomsheet',
    templateUrl: './maps-list-bottomsheet.component.html',
    styleUrls: ['./maps-list-bottomsheet.component.scss'],
    imports: [MatActionList, MatListItem, MatIcon, CdkCopyToClipboard, TranslateModule]
})
export class MapsListBottomsheetComponent implements OnInit {
  dialogRef = inject<MatBottomSheetRef<MapsListBottomsheetComponent>>(MatBottomSheetRef);
  data = inject(MAT_BOTTOM_SHEET_DATA);

  map: Map;
  constructor() {
    const data = this.data;

    this.map = data.map;
  }

  ngOnInit(): void {
  }
  getLink() {
    return location.origin + '/link/' + this.map.sharingLinkName;
  }
  view() {
    this.dialogRef.dismiss(MapListActions.View);
  }
  delete() {
    this.dialogRef.dismiss(MapListActions.Delete);
  }
  edit() {
    this.dialogRef.dismiss(MapListActions.Edit);
  }
}
