import { Component, Inject, OnInit, viewChild } from "@angular/core";
import { UntypedFormBuilder, UntypedFormGroup, Validators, FormsModule, ReactiveFormsModule } from "@angular/forms";
import { MatCheckboxChange, MatCheckbox } from "@angular/material/checkbox";
import { DateAdapter } from "@angular/material/core";
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from "@angular/material/dialog";
import { MatSelectionList } from "@angular/material/list";
import { Region } from "src/app/models/region.model";
import { StationMap } from "src/app/models/stationMap.model";
import { ApiService } from "src/app/services/api.service";
import { RegionsService } from "src/app/services/regions.service";
import { TranslationService } from "src/app/services/translation.service";
import { CdkScrollable } from "@angular/cdk/scrolling";
import { MatFormField, MatLabel } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { MatExpansionPanel, MatExpansionPanelHeader } from "@angular/material/expansion";
import { TooltipComponent, MatTooltip } from "@angular/material/tooltip";
import { MatButton } from "@angular/material/button";
import { TranslateModule } from "@ngx-translate/core";

@Component({
    selector: "app-station-maps-edit",
    templateUrl: "./station-maps-edit.component.html",
    styleUrls: ["./station-maps-edit.component.scss"],
    imports: [
        MatDialogTitle,
        FormsModule,
        ReactiveFormsModule,
        CdkScrollable,
        MatDialogContent,
        MatFormField,
        MatLabel,
        MatInput,
        MatExpansionPanel,
        MatExpansionPanelHeader,
        MatCheckbox,
        TooltipComponent,
        MatTooltip,
        MatDialogActions,
        MatButton,
        TranslateModule,
    ]
})
export class StationMapsEditComponent implements OnInit {
  form: UntypedFormGroup;
  regions: Region[];
  readonly regionsSelection = viewChild<MatSelectionList>("regionsSelection");
  selectedOptions: number[] = [];
  map: StationMap = {} as StationMap;
  constructor(
    private apiService: ApiService,
    private regionsService: RegionsService,
    private translationService: TranslationService,
    private formBuilder: UntypedFormBuilder,
    private dateAdapter: DateAdapter<any>,
    private dialogRef: MatDialogRef<StationMapsEditComponent>,
    @Inject(MAT_DIALOG_DATA) data
  ) {
    if (!!data && !!data.map) {
      this.map = data.map || ({} as StationMap);
      this.selectedOptions = this.map.regionIds || [];
    }
    this.form = this.formBuilder.group({
      name: [this.map?.name ?? "", Validators.required],
      nameNL: this.map?.nameNL ?? "",
      sharingLinkName: this.map?.sharingLinkName ?? "",
    });
  }

  ngOnInit() {
    this.translationService.languageChanged.subscribe(() => {
      this.sortOrder();
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.regionsService.getRegionsWithStations().subscribe((regions) => {
      this.regions = regions;
      this.sortOrder();
    });
  }
  sortOrder() {
    this.regions = this.regions.sort((a, b) => {
      if (this.name(a) > this.name(b)) {
        return 1;
      }
      if (this.name(a) < this.name(b)) {
        return -1;
      }
      return 0;
    });
  }

  onSubmit() {
    if (this.form.invalid || this.selectedOptions.length < 1) {
      return;
    }
    this.map.name = this.form.value.name;
    this.map.nameNL = this.form.value.nameNL;
    this.map.sharingLinkName = this.form.value.sharingLinkName;
    this.map.regionIds = this.selectedOptions;
    if (this.map.id) {
      this.apiService.updateStationMap(this.map).subscribe(() => {
        this.dialogRef.close(true);
      });
    } else {
      this.apiService.addStationMap(this.map).subscribe(() => {
        this.dialogRef.close(true);
      });
    }
  }

  goBack(): void {
    this.dialogRef.close(false);
  }

  isRegionChecked(id: number) {
    return this.selectedOptions.includes(id);
  }

  setRegion(id: number, event: MatCheckboxChange, subRegions?: Region[]) {
    if (event.checked && !this.selectedOptions.includes(id)) {
      this.selectedOptions.push(id);
      this.selectedOptions = this.selectedOptions.filter(
        (i) => !subRegions.map((r) => r.id).includes(i)
      );
    }
    if (!event.checked && this.selectedOptions.includes(id)) {
      this.selectedOptions = this.selectedOptions.filter((i) => i !== id);
      this.selectedOptions = this.selectedOptions.filter(
        (i) => !subRegions.map((r) => r.id).includes(i)
      );
    }
  }

  anyChecked(regions: Region[]) {
    return regions.some((r) => this.selectedOptions.includes(r.id));
  }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }
}
