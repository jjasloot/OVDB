import { Component, OnInit, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CountryAddComponent } from '../country-add/country-add.component';
import { ApiService } from 'src/app/services/api.service';
import { Country } from 'src/app/models/country.model';
import { Map } from 'src/app/models/map.model';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-maps-add',
  templateUrl: './maps-add.component.html',
  styleUrls: ['./maps-add.component.scss']
})
export class MapsAddComponent implements OnInit {
  mapName: string;
  mapSharingLink: string;
  mapDefault: boolean;

  id: number;
  loading: boolean;
  error: string;
  constructor(
    public dialogRef: MatDialogRef<MapsAddComponent>,
    private translateService: TranslateService,
    private apiService: ApiService,
    @Inject(MAT_DIALOG_DATA) data) {
    if (!!data && data.map) {
      const map = data.map as Map;
      this.mapName = map.name;
      this.id = map.mapId;
      this.mapSharingLink = map.sharingLinkName;
      this.mapDefault = map.default;
    }
  }

  ngOnInit() {

  }

  cancel() {
    this.dialogRef.close();
  }

  createMap(): Map {
    const map = ({
      name: this.mapName,
      default: this.mapDefault,
      sharingLinkName: this.mapSharingLink
    } as Map);
    if (!!this.id) {
      map.mapId = this.id;
    }
    return map
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