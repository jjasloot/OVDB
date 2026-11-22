import { Component, OnInit, inject, signal, computed, effect, ChangeDetectionStrategy, DestroyRef } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatCheckboxChange, MatCheckbox } from "@angular/material/checkbox";
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from "@angular/material/dialog";
import { FilterSettings, PropertyCondition } from "../models/filterSettings";
import { MatSelect } from "@angular/material/select";
import { MatOption } from "@angular/material/core";
import { MatAutocomplete, MatAutocompleteTrigger } from "@angular/material/autocomplete";
import { MatChip, MatChipRemove, MatChipSet } from "@angular/material/chips";
import { MatIcon } from "@angular/material/icon";
import { ApiService } from "../services/api.service";
import { Country } from "../models/country.model";
import { RouteType } from "../models/routeType.model";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { TranslationService } from "../services/translation.service";
import { DateAdapter } from "@angular/material/core";
import { DateTime } from "luxon";
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
  changeDetection: ChangeDetectionStrategy.Eager,
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
    MatSelect,
    MatOption,
    MatAutocomplete,
    MatAutocompleteTrigger,
    MatChipSet,
    MatChip,
    MatChipRemove,
    MatIcon,
  ]
})
export class MapFilterComponent implements OnInit {
  private apiService = inject(ApiService);
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private destroyRef = inject(DestroyRef);
  private dateAdapter = inject<DateAdapter<any>>(DateAdapter);
  dialogRef = inject<MatDialogRef<MapFilterComponent>>(MatDialogRef);
  private regionsService = inject(RegionsService);
  data = inject(MAT_DIALOG_DATA);

  settings: FilterSettings;
  regions = signal<Region[]>([]);
  selectedCountries = signal<number[]>([]);
  selectedTypes = signal<number[]>([]);
  selectedOperators = signal<number[]>([]);

  // Trip property conditions: pick a property, then a value (or yes/no), and add it as a chip.
  properties = signal<{ key: string; isBool: boolean; values: string[] }[]>([]);
  selectedProperties = signal<PropertyCondition[]>([]);
  newPropertyKey = signal<string | null>(null);
  newPropertyValue = signal("");
  newPropertyBool = signal(true);
  newProperty = computed(() => this.properties().find((p) => p.key === this.newPropertyKey()) ?? null);
  propertyValueOptions = computed(() => {
    const text = this.newPropertyValue().toLocaleLowerCase();
    return (this.newProperty()?.values ?? []).filter((v) => v.toLocaleLowerCase().includes(text)).slice(0, 20);
  });

  addProperty() {
    const property = this.newProperty();
    if (!property) {
      return;
    }
    const condition: PropertyCondition = property.isBool
      ? { key: property.key, bool: this.newPropertyBool() }
      : { key: property.key, value: this.newPropertyValue().trim() };
    if (!property.isBool && !condition.value) {
      return;
    }
    this.selectedProperties.update((list) => [...list, condition]);
    this.newPropertyValue.set("");
  }

  removeProperty(index: number) {
    this.selectedProperties.update((list) => list.filter((_, i) => i !== index));
  }

  propertyLabel(condition: PropertyCondition) {
    const value = condition.bool === undefined || condition.bool === null
      ? condition.value
      : this.translateService.instant(condition.bool ? "YES" : "NO");
    return condition.key + ": " + value;
  }
  operators = signal<{ id: number; name: string }[]>([]);
  selectedYears = signal<number[]>([]);
  to = signal<DateTime | null>(null);
  from = signal<DateTime | null>(null);
  routeTypes = signal<RouteType[]>([]);
  years = signal<number[]>([]);
  ownMap = signal(false);
  guid = signal<string>("");
  includeLineColours = signal(true);
  limitToSelectedAreas = signal(false);

  regionsString = computed(() => {
    return (
      this.selectedCountries().length +
      " " +
      this.translateService.instant("FILTER.SELECTED")
    );
  });

  typesString = computed(() => {
    const typesNames = this.routeTypes()
      .filter((c) => this.selectedTypes().includes(c.typeId!))
      .map((c) => this.name(c));
    let typesString = typesNames.join(", ");
    if (typesNames.length > 2) {
      typesString =
        typesNames.length +
        " " +
        this.translateService.instant("FILTER.SELECTED");
    }
    return typesString;
  });

  operatorsString = computed(() => {
    const names = this.operators().filter((o) => this.selectedOperators().includes(o.id)).map((o) => o.name);
    return names.length > 2 ? names.length + " " + this.translateService.instant("FILTER.SELECTED") : names.join(", ");
  });

  setOperator(id: number, checked: boolean) {
    this.selectedOperators.update((ids) => (checked ? [...ids.filter((i) => i !== id), id] : ids.filter((i) => i !== id)));
  }

  yearsString = computed(() => {
    const displayedYears = this.selectedYears().map((y) => this.displayYear(y));
    let yearsString = displayedYears.join(", ");
    if (displayedYears.length > 3) {
      yearsString =
        displayedYears.length +
        " " +
        this.translateService.instant("FILTER.SELECTED");
    }
    return yearsString;
  });

  constructor() {
    const data = this.data;

    this.settings = data.settings;
    this.ownMap.set(data.ownMap);
    this.guid.set(data.guid);
  }

  ngOnInit() {
    this.dateAdapter.setLocale(this.translationService.dateLocale);
    this.translationService.languageChanged.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.sortNames();
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.selectedCountries.set(this.settings.selectedCountries);
    this.selectedTypes.set(this.settings.selectedTypes);
    this.selectedOperators.set(this.settings.selectedOperators ?? []);
    this.selectedProperties.set(this.settings.selectedProperties ?? []);
    this.selectedYears.set(this.settings.selectedYears);
    this.from.set(this.settings.from);
    this.to.set(this.settings.to);
    this.includeLineColours.set(this.settings.includeLineColours);
    this.limitToSelectedAreas.set(this.settings.limitToSelectedAreas);
    this.regionsService.getMapsRegions(this.guid()).subscribe((regions) => {
      this.regions.set(regions);
      this.sortNames();
    });

    this.apiService.getTypes(this.guid()).subscribe((types) => {
      this.routeTypes.set(types);
    });
    this.apiService.getMapOperators(this.guid()).subscribe((operators) => this.operators.set(operators));
    this.apiService.getMapProperties(this.guid()).subscribe((properties) => this.properties.set(properties));
    this.apiService.getYears(this.guid()).subscribe((types) => {
      this.years.set(types.sort().reverse());
    });
  }
  sortNames() {
    if (this.regions()) {
      this.regions.update((regions) =>
        regions.sort((a, b) => {
          if (this.name(a) > this.name(b)) {
            return 1;
          }
          if (this.name(a) < this.name(b)) {
            return -1;
          }
          return 0;
        })
      );

      this.regions().forEach((region) => {
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
    if (this.selectedCountries().length === 0) {
      this.limitToSelectedAreas.set(false);
    }
    if (this.from() !== null && this.from()!.isValid) {
      const settings = new FilterSettings(
        "filter",
        this.includeLineColours(),
        this.limitToSelectedAreas(),
        this.from()!,
        // A start date alone means "from then on"; the map only applies a range with both ends.
        this.to() ?? DateTime.now(),
        this.selectedCountries(),
        this.selectedTypes(),
        [],
        this.selectedOperators(),
        this.selectedProperties()
      );
      this.dialogRef.close(settings);
    } else {
      const settings = new FilterSettings(
        "filter",
        this.includeLineColours(),
        this.limitToSelectedAreas(),
        undefined,
        undefined,
        this.selectedCountries(),
        this.selectedTypes(),
        this.selectedYears(),
        this.selectedOperators(),
        this.selectedProperties()
      );
      this.dialogRef.close(settings);
    }
  }

  isCountryChecked(id: number) {
    return this.selectedCountries().includes(id);
  }

  setCountry(id: number, event: MatCheckboxChange, subRegions?: Region[]) {
    if (event.checked && !this.selectedCountries().includes(id)) {
      this.selectedCountries.update((countries) => [
        ...countries,
        id,
      ]);
      this.selectedCountries.update((countries) =>
        countries.filter(
          (i) => !(subRegions ?? []).map((r) => r.id).includes(i)
        )
      );
    }
    if (!event.checked && this.selectedCountries().includes(id)) {
      this.selectedCountries.update((countries) =>
        countries.filter((i) => i !== id)
      );
      this.selectedCountries.update((countries) =>
        countries.filter(
          (i) => !(subRegions ?? []).map((r) => r.id).includes(i)
        )
      );
    }
  }


  anyChecked(regions: Region[]) {
    return regions.some((r) => this.selectedCountries().includes(r.id) || r.subRegions.some(sr => this.selectedCountries().includes(sr.id)));
  }

  isTypeChecked(id: number) {
    return this.selectedTypes().includes(id);
  }

  setType(id: number, event: MatCheckboxChange) {
    if (event.checked && !this.selectedTypes().includes(id)) {
      this.selectedTypes.update((types) => [...types, id]);
    }
    if (!event.checked && this.selectedTypes().includes(id)) {
      this.selectedTypes.update((types) =>
        types.filter((i) => i !== id)
      );
    }
  }

  isYearChecked(id: number) {
    return this.selectedYears().includes(id);
  }

  setYear(year: number, event: MatCheckboxChange) {
    if (event.checked && !this.selectedYears().includes(year)) {
      this.selectedYears.update((years) => [...years, year]);
    }
    if (!event.checked && this.selectedYears().includes(year)) {
      this.selectedYears.update((years) =>
        years.filter((i) => i !== year)
      );
    }
  }



  displayYear(year: number) {
    if (year) {
      return year;
    }
    return this.translateService.instant("FILTER.UNKNOWNYEAR");
  }

  resetYears() {
    this.selectedYears.set([]);
  }

  anySubRegionHasSubSubRegions(region: Region) {
    return region.subRegions.some(sr => sr.subRegions.length > 0)
  }
}
