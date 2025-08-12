import { Component, OnInit, Inject } from "@angular/core";
import { MatCheckboxChange, MatCheckbox } from "@angular/material/checkbox";
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from "@angular/material/dialog";
import { FilterSettings } from "../models/filterSettings";
import { ApiService } from "../services/api.service";
import { Country } from "../models/country.model";
import { RouteType } from "../models/routeType.model";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { TranslationService } from "../services/translation.service";
import { DateAdapter } from "@angular/material/core";
import { Moment } from "moment";
import { RegionsService } from "../services/regions.service";
import { Region } from "../models/region.model";
import { CdkScrollable } from "@angular/cdk/scrolling";
import { MatAccordion, MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatExpansionPanelDescription } from "@angular/material/expansion";
import { TooltipComponent, MatTooltip } from "@angular/material/tooltip";
import { MatFormField, MatLabel, MatSuffix } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { MatDatepickerInput, MatDatepickerToggle, MatDatepicker } from "@angular/material/datepicker";
import { FormsModule } from "@angular/forms";
import { MatButton } from "@angular/material/button";

@Component({
  selector: "app-map-filter",
  templateUrl: "./map-filter.component.html",
  styleUrls: ["./map-filter.component.scss"],
  imports: [
    MatDialogTitle,
    CdkScrollable,
    MatDialogContent,
    MatAccordion,
    MatExpansionPanel,
    MatExpansionPanelHeader,
    MatExpansionPanelTitle,
    MatExpansionPanelDescription,
    MatCheckbox,
    TooltipComponent,
    MatTooltip,
    MatFormField,
    MatLabel,
    MatInput,
    MatDatepickerInput,
    FormsModule,
    MatDatepickerToggle,
    MatSuffix,
    MatDatepicker,
    MatDialogActions,
    MatButton,
    TranslateModule,
  ]
})
export class MapFilterComponent implements OnInit {
  settings: FilterSettings;
  regions: Region[] = [];
  selectedCountries: number[] = [];
  selectedTypes: number[] = [];
  selectedYears: number[] = [];
  to: Moment;
  from: Moment;
  routeTypes: RouteType[];
  years: number[];
  ownMap = false;
  guid: string;
  includeLineColours = true;
  limitToSelectedAreas = false;
  constructor(
    private apiService: ApiService,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private dateAdapter: DateAdapter<any>,
    public dialogRef: MatDialogRef<MapFilterComponent>,
    private regionsService: RegionsService,
    @Inject(MAT_DIALOG_DATA) public data
  ) {
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
    this.includeLineColours = this.settings.includeLineColours;
    this.limitToSelectedAreas = this.settings.limitToSelectedAreas;
    this.regionsService.getMapsRegions(this.guid).subscribe((regions) => {
      this.regions = regions;
      this.sortNames();
    });

    this.apiService.getTypes(this.guid).subscribe((types) => {
      this.routeTypes = types;
    });
    this.apiService.getYears(this.guid).subscribe((types) => {
      this.years = types.sort().reverse();
    });
  }
  sortNames() {
    if (this.regions) {
      this.regions = this.regions.sort((a, b) => {
        if (this.name(a) > this.name(b)) {
          return 1;
        }
        if (this.name(a) < this.name(b)) {
          return -1;
        }
        return 0;
      });

      this.regions.forEach((region) => {
        region.subRegions = region.subRegions.sort((a, b) => {
          if (this.name(a) > this.name(b)) {
            return 1;
          }
          if (this.name(a) < this.name(b)) {
            return -1;
          }
          return 0;
        });
      });
    }
  }
  name(item: Country | RouteType | Region) {
    return this.translationService.getNameForItem(item);
  }
  cancel() {
    this.dialogRef.close();
  }

  return() {
    if (this.selectedCountries.length === 0) {
      this.limitToSelectedAreas = false;
    }
    if (this.from !== null && this.from.isValid()) {
      const settings = new FilterSettings(
        "filter",
        this.includeLineColours,
        this.limitToSelectedAreas,
        this.from,
        this.to,
        this.selectedCountries,
        this.selectedTypes,
        []
      );
      this.dialogRef.close(settings);
    } else {
      const settings = new FilterSettings(
        "filter",
        this.includeLineColours,
        this.limitToSelectedAreas,
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

  setCountry(id: number, event: MatCheckboxChange, subRegions?: Region[]) {
    if (event.checked && !this.selectedCountries.includes(id)) {
      this.selectedCountries.push(id);
      this.selectedCountries = this.selectedCountries.filter(
        (i) => !subRegions.map((r) => r.id).includes(i)
      );
    }
    if (!event.checked && this.selectedCountries.includes(id)) {
      this.selectedCountries = this.selectedCountries.filter((i) => i !== id);
      this.selectedCountries = this.selectedCountries.filter(
        (i) => !subRegions.map((r) => r.id).includes(i)
      );
    }


  }


  anyChecked(regions: Region[]) {
    return regions.some((r) => this.selectedCountries.includes(r.id) || r.subRegions.some(sr => this.selectedCountries.includes(sr.id)));
  }

  isTypeChecked(id: number) {
    return this.selectedTypes.includes(id);
  }

  setType(id: number, event: MatCheckboxChange) {
    if (event.checked && !this.selectedTypes.includes(id)) {
      this.selectedTypes.push(id);
    }
    if (!event.checked && this.selectedTypes.includes(id)) {
      this.selectedTypes = this.selectedTypes.filter((i) => i !== id);
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
      this.selectedYears = this.selectedYears.filter((i) => i !== year);
    }
  }

  get regionsString(): string {
    return (
      this.selectedCountries.length +
      " " +
      this.translateService.instant("FILTER.SELECTED")
    );
  }

  get typesString(): string {
    const typesNames = this.routeTypes
      .filter((c) => this.selectedTypes.includes(c.typeId))
      .map((c) => this.name(c));
    let typesString = typesNames.join(", ");
    if (typesNames.length > 2) {
      typesString =
        typesNames.length +
        " " +
        this.translateService.instant("FILTER.SELECTED");
    }
    return typesString;
  }

  get yearsString(): string {
    const displayedYears = this.selectedYears.map((y) => this.displayYear(y));
    let yearsString = displayedYears.join(", ");
    if (displayedYears.length > 3) {
      yearsString =
        displayedYears.length +
        " " +
        this.translateService.instant("FILTER.SELECTED");
    }
    return yearsString;
  }

  displayYear(year: number) {
    if (year) {
      return year;
    }
    return this.translateService.instant("FILTER.UNKNOWNYEAR");
  }

  resetYears() {
    this.selectedYears = [];
  }

  anySubRegionHasSubSubRegions(region: Region) {
    return region.subRegions.some(sr => sr.subRegions.length > 0)
  }
}
