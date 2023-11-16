import { Component, OnInit, Inject } from '@angular/core';
import { MatCheckboxChange } from '@angular/material/checkbox';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { FilterSettings } from '../models/filterSettings';
import { ApiService } from '../services/api.service';
import { Country } from '../models/country.model';
import { RouteType } from '../models/routeType.model';
import { TranslateService } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { DateAdapter } from '@angular/material/core';
import { Moment } from 'moment';

@Component({
  selector: 'app-map-filter',
  templateUrl: './map-filter.component.html',
  styleUrls: ['./map-filter.component.scss']
})
export class MapFilterComponent implements OnInit {
  settings: FilterSettings;
  countries: Country[];
  selectedCountries: number[] = [];
  selectedTypes: number[] = [];
  selectedYears: number[] = [];
  to: Moment;
  from: Moment;
  routeTypes: RouteType[];
  years: number[];
  ownMap = false;
  guid: string;
  constructor(
    private apiService: ApiService,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private dateAdapter: DateAdapter<any>,
    public dialogRef: MatDialogRef<MapFilterComponent>,
    @Inject(MAT_DIALOG_DATA) public data) {
    this.settings = data.settings;
    this.ownMap = data.ownMap;
    this.guid = data.guid;
  }

  ngOnInit() {
    this.dateAdapter.setLocale(this.translationService.dateLocale);
    this.translationService.languageChanged.subscribe(() => {
      this.sortNames();
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.selectedCountries = this.settings.selectedCountries;
    this.selectedTypes = this.settings.selectedTypes;
    this.selectedYears = this.settings.selectedYears;
    this.from = this.settings.from;
    this.to = this.settings.to;
    this.apiService.getCountries(this.guid).subscribe(countries => {
      this.countries = countries;
      this.sortNames();
    });
    this.apiService.getTypes(this.guid).subscribe(types => {
      this.routeTypes = types;
    });
    this.apiService.getYears(this.guid).subscribe(types => {
      this.years = types.sort().reverse();
    });
  }
  sortNames() {
    if (!!this.countries) {
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
  }
  name(item: Country | RouteType) {
    return this.translationService.getNameForItem(item);
  }
  cancel() {
    this.dialogRef.close();
  }

  return() {
    if (this.from !== null && this.from.isValid()) {
      const settings = new FilterSettings(
        'filter',
        this.from,
        this.to,
        this.selectedCountries,
        this.selectedTypes,
        []
      );
      this.dialogRef.close(settings);
    } else {
      const settings = new FilterSettings(
        'filter',
        null,
        null,
        this.selectedCountries,
        this.selectedTypes,
        this.selectedYears
      );
      this.dialogRef.close(settings);
    }

  }

  isCountryChecked(id: number) {
    return this.selectedCountries.includes(id);
  }

  setCountry(id: number, event: MatCheckboxChange) {
    if (event.checked && !this.selectedCountries.includes(id)) {
      this.selectedCountries.push(id);
    }
    if (!event.checked && this.selectedCountries.includes(id)) {
      this.selectedCountries = this.selectedCountries.filter(i => i !== id);
    }
  }

  isTypeChecked(id: number) {
    return this.selectedTypes.includes(id);
  }

  setType(id: number, event: MatCheckboxChange) {
    if (event.checked && !this.selectedTypes.includes(id)) {
      this.selectedTypes.push(id);
    }
    if (!event.checked && this.selectedTypes.includes(id)) {
      this.selectedTypes = this.selectedTypes.filter(i => i !== id);
    }
  }

  isYearChecked(id: number) {
    return this.selectedYears.includes(id);
  }

  setYear(year: number, event: MatCheckboxChange) {
    if (event.checked && !this.selectedYears.includes(year)) {
      this.selectedYears.push(year);
    }
    if (!event.checked && this.selectedYears.includes(year)) {
      this.selectedYears = this.selectedYears.filter(i => i !== year);
    }
  }

  get countriesString(): string {
    const countriesNames = this.countries.filter(c => this.selectedCountries.includes(c.countryId)).map(c => this.name(c));
    let countriesString = countriesNames.join(', ');
    if (countriesNames.length > 2) {
      countriesString = countriesNames.length + ' ' + this.translateService.instant('FILTER.SELECTED');
    }
    return countriesString;
  }

  get typesString(): string {
    const typesNames = this.routeTypes.filter(c => this.selectedTypes.includes(c.typeId)).map(c => this.name(c));
    let typesString = typesNames.join(', ');
    if (typesNames.length > 2) {
      typesString = typesNames.length + ' ' + this.translateService.instant('FILTER.SELECTED');
    }
    return typesString;
  }

  get yearsString(): string {
    const displayedYears = this.selectedYears.map(y => this.displayYear(y));
    let yearsString = displayedYears.join(', ');
    if (displayedYears.length > 3) {
      yearsString = displayedYears.length + ' ' + this.translateService.instant('FILTER.SELECTED');
    }
    return yearsString;
  }

  displayYear(year: number) {
    if (!!year) {
      return year;
    }
    return this.translateService.instant('FILTER.UNKNOWNYEAR');
  }

  resetYears() {
    this.selectedYears = [];
  }
}
