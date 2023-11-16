import { Component, OnInit, Inject, ViewChild } from '@angular/core';
import { MatLegacyDialogRef as MatDialogRef, MAT_LEGACY_DIALOG_DATA as MAT_DIALOG_DATA } from '@angular/material/legacy-dialog';
import { ApiService } from 'src/app/services/api.service';
import { TranslateService } from '@ngx-translate/core';
import { RouteType } from 'src/app/models/routeType.model';
import { Country } from 'src/app/models/country.model';
import { TranslationService } from 'src/app/services/translation.service';
import { DateAdapter } from '@angular/material/core';
import { Map } from 'src/app/models/map.model';
import { MatLegacySelectionList as MatSelectionList } from '@angular/material/legacy-list';
import { MatAccordionTogglePosition } from '@angular/material/expansion';
import { MultipleEdit } from 'src/app/models/multipleEdit.model';


@Component({
  selector: 'app-edit-multiple',
  templateUrl: './edit-multiple.component.html',
  styleUrls: ['./edit-multiple.component.scss']
})
export class EditMultipleComponent implements OnInit {
  selectedRoutes: number[];
  types: RouteType[];
  countries: Country[];
  maps: Map[];
  @ViewChild('countriesSelection') countriesSelection: MatSelectionList;
  @ViewChild('mapsSelection') mapsSelection: MatSelectionList;
  firstDateTime: Date;
  routeTypeId: number;
  updateDate = false;
  updateType = false;
  updateCountries = false;
  updateMaps = false;
  error: any;

  constructor(public dialogRef: MatDialogRef<EditMultipleComponent>,
              private translationService: TranslationService,
              private translateService: TranslateService,
              private dateAdapter: DateAdapter<any>,
              private apiService: ApiService,
              @Inject(MAT_DIALOG_DATA) data) {
    if (!!data && data.selectedRoutes) {
      this.selectedRoutes = data.selectedRoutes;
    }
  }

  ngOnInit(): void {
    this.translationService.languageChanged.subscribe(() => {
      this.sortOrder();
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.apiService.getTypes().subscribe(types => {
      this.types = types;
    });
    this.apiService.getCountries().subscribe(countries => {
      this.countries = countries;
      this.sortOrder();
    });
    this.apiService.getMaps().subscribe(maps => {
      this.maps = maps;
    });
  }

  sortOrder() {
    this.countries = this.countries.sort((a, b) => {
      if (this.name(a) > this.name(b)) {
        return 1;
      }
      if (this.name(a) < this.name(b)) {
        return -1;
      }
      return 0;
    });
  }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }
  get countriesString() {
    if (!this.countriesSelection || !this.countries) {
      return '';
    }
    const countries = this.countries
      .filter(c => this.countriesSelection.selectedOptions.selected.some(rc => rc.value === c.countryId))
      .map(c => this.name(c));
    if (countries.length > 3) {
      return '' + countries.length + ' ' + this.translateService.instant('ROUTEDETAILS.COUNTRIESINSTRING');
    }
    return countries.join(', ');
  }

  get mapsString() {
    if (!this.mapsSelection || !this.maps) {
      return '';
    }
    const maps = this.maps
      .filter(m => this.mapsSelection.selectedOptions.selected.some(rm => rm.value === m.mapId))
      .map(m => this.name(m));
    if (maps.length > 3) {
      return '' + maps.length + ' ' + this.translateService.instant('ROUTEDETAILS.MAPSINSTRING');
    }
    return maps.join(', ');
  }

  cancel() {
    this.dialogRef.close();
  }

  return() {
    const model = {
      date: this.firstDateTime,
      routeIds: this.selectedRoutes,
      typeId: this.routeTypeId,
      updateCountries: this.updateCountries,
      updateDate: this.updateDate,
      updateMaps: this.updateMaps,
      updateType: this.updateType

    } as MultipleEdit;
    if (this.updateCountries) {
      model.countries = this.countriesSelection.selectedOptions.selected.map(s => s.value);
    }
    if (this.updateMaps) {
      model.maps = this.mapsSelection.selectedOptions.selected.map(s => s.value);
    }
    this.apiService.updateMultiple(model).subscribe(() => { this.dialogRef.close(); }, err => { this.error = err.error; });
  }
}
