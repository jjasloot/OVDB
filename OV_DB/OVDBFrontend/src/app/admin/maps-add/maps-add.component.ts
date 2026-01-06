import { Component, OnInit, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { CountryAddComponent } from '../country-add/country-add.component';
import { ApiService } from 'src/app/services/api.service';
import { Country } from 'src/app/models/country.model';
import { Map } from 'src/app/models/map.model';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { CdkScrollable } from '@angular/cdk/scrolling';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatButton } from '@angular/material/button';

@Component({
    selector: 'app-maps-add',
    templateUrl: './maps-add.component.html',
    styleUrls: ['./maps-add.component.scss'],
    imports: [MatDialogTitle, CdkScrollable, MatDialogContent, MatFormField, MatLabel, MatInput, FormsModule, MatCheckbox, MatDialogActions, MatButton, TranslateModule]
})
export class MapsAddComponent implements OnInit {
  dialogRef = inject<MatDialogRef<MapsAddComponent>>(MatDialogRef);
  private translateService = inject(TranslateService);
  private apiService = inject(ApiService);

  mapName: string;
  mapSharingLink: string;
  mapDefault: boolean;
  showRouteInfo = true;
  showRouteOutline = true;
  mapCompleted = false;

  id: number;
  loading: boolean;
  error: string;
  constructor() {
    const data = inject(MAT_DIALOG_DATA);

    if (!!data && data.map) {
      const map = data.map as Map;
      this.mapName = map.name;
      this.id = map.mapId;
      this.mapSharingLink = map.sharingLinkName;
      this.mapDefault = map.default;
      this.showRouteInfo = map.showRouteInfo;
      this.showRouteOutline = map.showRouteOutline;
      this.mapCompleted = map.completed;
    }
  }

  ngOnInit() { }

  cancel() {
    this.dialogRef.close();
  }

  createMap(): Map {
    const map = ({
      name: this.mapName,
      default: this.mapDefault,
      sharingLinkName: this.mapSharingLink,
      showRouteInfo: this.showRouteInfo,
      showRouteOutline: this.showRouteOutline,
      completed: this.mapCompleted
    } as Map);
    if (this.id) {
      map.mapId = this.id;
    }
    return map;
  }

  return() {
    if (!this.mapName || this.loading) {
      return;
    }
    this.loading = true;
    if (!this.id) {
      this.apiService.addMap(this.createMap()).subscribe(() => {
        this.dialogRef.close(true);
      }, err => {
        this.error = this.translateService.instant('ADDTYPE.ERROR') + ' ' + err.error;
      });
    } else {
      this.apiService.updateMap(this.createMap()).subscribe(() => {
        this.dialogRef.close(true);
      }, err => {
        this.error = this.translateService.instant('ADDTYPE.ERROR') + ' ' + err.error;
      });
    }

  }


}
