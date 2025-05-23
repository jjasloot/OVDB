import { Component, OnInit, Inject, viewChild } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { ApiService } from 'src/app/services/api.service';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { RouteType } from 'src/app/models/routeType.model';
import { Country } from 'src/app/models/country.model';
import { TranslationService } from 'src/app/services/translation.service';
import { DateAdapter, MatOption } from '@angular/material/core';
import { Map } from 'src/app/models/map.model';
import { MatSelectionList, MatListOption } from '@angular/material/list';
import { MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatExpansionPanelDescription } from '@angular/material/expansion';
import { MultipleEdit } from 'src/app/models/multipleEdit.model';
import { CdkScrollable } from '@angular/cdk/scrolling';
import { MatCheckbox } from '@angular/material/checkbox';
import { FormsModule } from '@angular/forms';
import { MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatDatepickerInput, MatDatepickerToggle, MatDatepicker } from '@angular/material/datepicker';
import { MatSelect } from '@angular/material/select';
import { MatButton } from '@angular/material/button';
import { JsonPipe } from '@angular/common';


@Component({
    selector: 'app-edit-multiple',
    templateUrl: './edit-multiple.component.html',
    styleUrls: ['./edit-multiple.component.scss'],
    imports: [MatDialogTitle, CdkScrollable, MatDialogContent, MatCheckbox, FormsModule, MatFormField, MatLabel, MatInput, MatDatepickerInput, MatDatepickerToggle, MatSuffix, MatDatepicker, MatSelect, MatOption, MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatExpansionPanelDescription, MatSelectionList, MatListOption, MatDialogActions, MatButton, JsonPipe, TranslateModule]
})
export class EditMultipleComponent implements OnInit {
  selectedRoutes: number[];
  types: RouteType[];
  countries: Country[];
  maps: Map[];
  readonly countriesSelection = viewChild<MatSelectionList>('countriesSelection');
  readonly mapsSelection = viewChild<MatSelectionList>('mapsSelection');
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
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.apiService.getTypes().subscribe(types => {
      this.types = types;
    });

    this.apiService.getMaps().subscribe(maps => {
      this.maps = maps;
    });
  }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }
  get countriesString() {
    if (!this.countriesSelection() || !this.countries) {
      return '';
    }
    const countries = this.countries
      .filter(c => this.countriesSelection().selectedOptions.selected.some(rc => rc.value === c.countryId))
      .map(c => this.name(c));
    if (countries.length > 3) {
      return '' + countries.length + ' ' + this.translateService.instant('ROUTEDETAILS.COUNTRIESINSTRING');
    }
    return countries.join(', ');
  }

  get mapsString() {
    if (!this.mapsSelection() || !this.maps) {
      return '';
    }
    const maps = this.maps
      .filter(m => this.mapsSelection().selectedOptions.selected.some(rm => rm.value === m.mapId))
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
      updateDate: this.updateDate,
      updateMaps: this.updateMaps,
      updateType: this.updateType

    } as MultipleEdit;
    if (this.updateMaps) {
      model.maps = this.mapsSelection().selectedOptions.selected.map(s => s.value);
    }
    this.apiService.updateMultiple(model).subscribe(() => { this.dialogRef.close(); }, err => { this.error = err.error; });
  }
}
