import { Component, OnInit } from '@angular/core';
import { Map } from 'src/app/models/map.model';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { DataUpdateService } from 'src/app/services/data-update.service';
import { AreYouSureDialogComponent } from 'src/app/are-you-sure-dialog/are-you-sure-dialog.component';
import { MapsAddComponent } from '../maps-add/maps-add.component';
import { TranslateService } from '@ngx-translate/core';
import { SortItemsDialogComponent } from '../sort-items-dialog/sort-items-dialog.component';
import { MatBottomSheet } from '@angular/material/bottom-sheet';
import { MapsListBottomsheetComponent } from './maps-list-bottomsheet/maps-list-bottomsheet.component';
import { MapListActions } from 'src/app/models/maps-list-actions.enum';

@Component({
  selector: 'app-maps-list',
  templateUrl: './maps-list.component.html',
  styleUrls: ['./maps-list.component.scss']
})
export class MapsListComponent implements OnInit {

  data: Map[];
  loading = false;

  constructor(
    private apiService: ApiService,
    private router: Router,
    private dialog: MatDialog,
    private translateService: TranslateService,
    private dataUpdateService: DataUpdateService,
    private bottomSheet: MatBottomSheet
  ) { }

  ngOnInit() {
    this.loadData();
  }

  private loadData() {
    this.loading = true;
    this.apiService.getMaps().subscribe(data => {
      this.data = data;
      this.loading = false;
    });
  }

  add() {
    const dialogRef = this.dialog.open(MapsAddComponent, {
      width: this.getWidth(),
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.loadData();
      }
    });
  }

  edit(map: Map) {
    const dialogRef = this.dialog.open(MapsAddComponent, {
      width: this.getWidth(),
      data: { map }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.loadData();
      }
    });
  }
  openBottomSheet(map: Map): void {
    const ref = this.bottomSheet.open(MapsListBottomsheetComponent, { data: { map } });
    ref.afterDismissed().subscribe((action: MapListActions) => {
      switch (action) {
        case MapListActions.View:
          this.view(map);
          return;
        case MapListActions.Delete:
          this.delete(map);
          return;
        case MapListActions.Edit:
          this.edit(map);
          return;
      }
    });
  }


  getLink(map: Map) {
    return location.origin + '/link/' + map.sharingLinkName;
  }
  view(map: Map) {
    this.router.navigate(['/map', map.mapGuid]);
  }
  delete(map: Map) {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      width: this.getWidth(),
      data: {
        item: this.translateService.instant('MAPLIST.DELETEFRONT') + ' ' + map.name + ' '
          + this.translateService.instant('MAPLIST.DELETEREAR')
      }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.apiService.deleteMap(map.mapId).subscribe(() => {
          this.loadData();
          this.dataUpdateService.requestUpdate();
        });
      }
    });
  }

  private getWidth() {
    let width = '90%';
    if (window.innerWidth > 600) {
      width = '50%';
    }
    return width;
  }

  sort() {
    const dialogRef = this.dialog.open(SortItemsDialogComponent, {
      width: this.getWidth(),
      data: {
        list: Object.assign([], this.data),
        title: this.translateService.instant('MAPLIST.SORTTITLE')
      }
    });
    dialogRef.afterClosed().subscribe((data: false | Map[]) => {
      if (data === false) {
        return;
      }
      this.apiService.updateMapOrder(data.map(d => d.mapId)).subscribe(() => this.loadData());


    });
  }
}
