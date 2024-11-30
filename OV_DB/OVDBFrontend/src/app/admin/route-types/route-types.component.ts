import { Component, OnInit } from '@angular/core';
import { RouteType } from 'src/app/models/routeType.model';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { AreYouSureDialogComponent } from 'src/app/are-you-sure-dialog/are-you-sure-dialog.component';
import { RouteTypesAddComponent } from '../route-types-add/route-types-add.component';
import { DataUpdateService } from 'src/app/services/data-update.service';
import { SortItemsDialogComponent } from '../sort-items-dialog/sort-items-dialog.component';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { TranslationService } from 'src/app/services/translation.service';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatList, MatListItem } from '@angular/material/list';
import { MatChipListbox, MatChipOption } from '@angular/material/chips';
import { MatButton, MatIconButton, MatFabButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';

@Component({
    selector: 'app-route-types',
    templateUrl: './route-types.component.html',
    styleUrls: ['./route-types.component.scss'],
    imports: [MatProgressSpinner, MatList, MatListItem, MatChipListbox, MatChipOption, MatButton, MatIconButton, MatIcon, MatFabButton, TranslateModule]
})
export class RouteTypesComponent implements OnInit {

  data: RouteType[];
  loading = false;

  constructor(
    private apiService: ApiService,
    private dialog: MatDialog,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private dataUpdateService: DataUpdateService) { }

  ngOnInit() {
    this.loadData();
  }

  private loadData() {
    this.loading = true;
    this.apiService.getTypes().subscribe(data => {
      this.data = data;
      this.loading = false;
    });
  }

  add() {
    const dialogRef = this.dialog.open(RouteTypesAddComponent, {
      width: this.getWidth(),
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.loadData();
      }
    });
  }
  sort() {
    const dialogRef = this.dialog.open(SortItemsDialogComponent, {
      width: this.getWidth(),
      data: {
        list: Object.assign([], this.data),
        title: this.translateService.instant('ROUTETYPES.SORTTITLE')
      }
    });
    dialogRef.afterClosed().subscribe((data: false | RouteType[]) => {
      if (data === false) {
        return;
      }
      this.apiService.updateRouteTypeOrder(data.map(d => d.typeId)).subscribe(() => this.loadData());


    });
  }
  name(routeType: RouteType) {
    return this.translationService.getNameForItem(routeType);
  }

  edit(routeType: RouteType) {
    const dialogRef = this.dialog.open(RouteTypesAddComponent, {
      width: this.getWidth(),
      data: { routeType }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.loadData();
      }
    });
  }
  delete(routeType: RouteType) {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      width: this.getWidth(),
      data: {
        item: this.translateService.instant('ROUTETYPES.DELETEFRONT') + ' ' + routeType.name + ' ' + this.translateService.instant('ROUTETYPES.DELETEBACK')
      }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.apiService.deleteRouteType(routeType.typeId).subscribe(() => {
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
}
