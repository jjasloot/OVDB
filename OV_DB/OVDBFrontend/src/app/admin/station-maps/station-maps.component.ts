import { Component, OnInit, inject } from "@angular/core";
import { MatBottomSheet } from "@angular/material/bottom-sheet";
import { MatDialog } from "@angular/material/dialog";
import { Router } from "@angular/router";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { AreYouSureDialogComponent } from "src/app/are-you-sure-dialog/are-you-sure-dialog.component";
import { STANDARD_DIALOG } from "src/app/constants/dialog-sizes";
import { MapListActions } from "src/app/models/maps-list-actions.enum";
import { StationMap } from "src/app/models/stationMap.model";
import { ApiService } from "src/app/services/api.service";
import { DataUpdateService } from "src/app/services/data-update.service";
import { TranslationService } from "src/app/services/translation.service";
import { MapsListBottomsheetComponent } from "../maps-list/maps-list-bottomsheet/maps-list-bottomsheet.component";
import { SortItemsDialogComponent } from "../sort-items-dialog/sort-items-dialog.component";
import { StationMapsEditComponent } from "../station-maps-edit/station-maps-edit.component";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatList, MatListItem } from "@angular/material/list";
import { MatIconButton, MatFabButton } from "@angular/material/button";
import { MatTooltip } from "@angular/material/tooltip";
import { MatIcon } from "@angular/material/icon";
import { CdkCopyToClipboard } from "@angular/cdk/clipboard";

@Component({
  selector: "app-station-maps",
  templateUrl: "./station-maps.component.html",
  styleUrls: ["./station-maps.component.scss"],
  imports: [
    MatProgressSpinner,
    MatList,
    MatListItem,
    MatIconButton,
    MatTooltip,
    MatIcon,
    CdkCopyToClipboard,
    MatFabButton,
    TranslateModule,
  ],
})
export class StationMapsComponent implements OnInit {
  private apiService = inject(ApiService);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private dataUpdateService = inject(DataUpdateService);
  private bottomSheet = inject(MatBottomSheet);

  data: StationMap[];
  loading = false;

  ngOnInit() {
    this.loadData();
  }

  private loadData() {
    this.loading = true;
    this.apiService.listStationMaps().subscribe({
      next: (data) => {
        this.data = data;
        this.loading = false;
      },
      error: () => (this.loading = false),
    });
  }

  getName(object) {
    return this.translationService.getNameForItem(object);
  }

  add() {
    const dialogRef = this.dialog.open(StationMapsEditComponent, {
      ...STANDARD_DIALOG,
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.loadData();
      }
    });
  }

  edit(map: StationMap) {
    const dialogRef = this.dialog.open(StationMapsEditComponent, {
      ...STANDARD_DIALOG,
      data: { map },
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.loadData();
      }
    });
  }
  openBottomSheet(stationMap: StationMap): void {
    const ref = this.bottomSheet.open(MapsListBottomsheetComponent, {
      data: { map: stationMap },
    });
    ref.afterDismissed().subscribe((action: MapListActions) => {
      switch (action) {
        case MapListActions.View:
          this.view(stationMap);
          return;
        case MapListActions.Delete:
          this.delete(stationMap);
          return;
        case MapListActions.Edit:
          this.edit(stationMap);
          return;
      }
    });
  }

  getLink(map: StationMap) {
    return location.origin + "/link/" + map.sharingLinkName;
  }
  view(map: StationMap) {
    this.router.navigate(["/stations/map", map.mapGuid]);
  }
  delete(map: StationMap) {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      ...STANDARD_DIALOG,
      data: {
        item:
          this.translateService.instant("STATIONMAP.DELETEFRONT") +
          " " +
          map.name +
          " " +
          this.translateService.instant("STATIONMAP.DELETEREAR"),
      },
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.apiService.deleteStationMap(map.id).subscribe(() => {
          this.loadData();
          this.dataUpdateService.requestUpdate();
        });
      }
    });
  }

  sort() {
    const dialogRef = this.dialog.open(SortItemsDialogComponent, {
      ...STANDARD_DIALOG,
      data: {
        list: Object.assign([], this.data),
        title: this.translateService.instant("MAPLIST.SORTTITLE"),
      },
    });
    dialogRef.afterClosed().subscribe((data: false | StationMap[]) => {
      if (data === false) {
        return;
      }
      this.apiService
        .updateStationMapOrder(data.map((d) => d.id))
        .subscribe(() => this.loadData());
    });
  }
}
