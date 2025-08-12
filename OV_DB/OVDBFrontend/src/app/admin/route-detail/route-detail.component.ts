import { Component, OnInit, signal, viewChild } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { Route } from "src/app/models/route.model";
import { ApiService } from "src/app/services/api.service";
import { UntypedFormBuilder, Validators, UntypedFormGroup, FormsModule, ReactiveFormsModule } from "@angular/forms";
import moment from "moment";
import { RouteType } from "src/app/models/routeType.model";
import { Country } from "src/app/models/country.model";
import { MatSelectionList, MatListOption } from "@angular/material/list";
import { UpdateRoute } from "src/app/models/updateRoute.model";
import { Map } from "src/app/models/map.model";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { DateAdapter, MatOption } from "@angular/material/core";
import { TranslationService } from "src/app/services/translation.service";
import { MatDialog } from "@angular/material/dialog";
import { AreYouSureDialogComponent } from "src/app/are-you-sure-dialog/are-you-sure-dialog.component";
import saveAs from "file-saver";
import { AuthenticationService } from "src/app/services/authentication.service";
import { OperatorService } from "src/app/services/operator.service";
import { MatButton } from "@angular/material/button";
import { MatFormField, MatLabel, MatSuffix } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { RouteDetailOperatorSelectionComponent } from "./route-detail-operator-selection/route-detail-operator-selection.component";
import { MatDatepickerInput, MatDatepickerToggle, MatDatepicker } from "@angular/material/datepicker";
import { MatSelect } from "@angular/material/select";
import { MatCard, MatCardHeader, MatCardSubtitle, MatCardContent } from "@angular/material/card";
import { MatChip } from "@angular/material/chips";
import { MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatExpansionPanelDescription } from "@angular/material/expansion";
import { DecimalPipe } from "@angular/common";

@Component({
    selector: "app-route-detail",
    templateUrl: "./route-detail.component.html",
    styleUrls: ["./route-detail.component.scss"],
    imports: [
        MatButton,
        FormsModule,
        ReactiveFormsModule,
        MatFormField,
        MatLabel,
        MatInput,
        RouteDetailOperatorSelectionComponent,
        MatDatepickerInput,
        MatDatepickerToggle,
        MatSuffix,
        MatDatepicker,
        MatSelect,
        MatOption,
        MatCard,
        MatCardHeader,
        MatCardSubtitle,
        MatCardContent,
        MatChip,
        MatExpansionPanel,
        MatExpansionPanelHeader,
        MatExpansionPanelTitle,
        MatExpansionPanelDescription,
        MatSelectionList,
        MatListOption,
        DecimalPipe,
        TranslateModule,
    ]
})
export class RouteDetailComponent implements OnInit {
  routeId: number;
  route: Route;
  form: UntypedFormGroup;
  types: RouteType[];
  countries: Country[];
  maps: Map[];
  activeOperators = signal<number[]>([]);
  logo = signal<string | null>(null);
  colour: string;

  readonly countriesSelection = viewChild<MatSelectionList>("countriesSelection");
  readonly mapsSelection = viewChild<MatSelectionList>("mapsSelection");

  selectedOptions: number[];
  selectedMaps: number[];

  constructor(
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private formBuilder: UntypedFormBuilder,
    private authService: AuthenticationService,
    private dateAdapter: DateAdapter<any>,
    private dialog: MatDialog,
    private router: Router,
  ) {
    this.dateAdapter.setLocale(this.translationService.dateLocale);

    this.form = this.formBuilder.group({
      name: ["", Validators.required],
      nameNL: "",
      description: "",
      descriptionNL: "",
      from: "",
      to: "",
      lineNumber: null,
      operatingCompany: null,
      overrideColour: null,
      firstDateTime: "",
      routeTypeId: [null, Validators.required],
      calculatedDistance: [null],
      overrideDistance: [null],
      intermediateRegion: null, // Added for intermediate region
    });
  }

  ngOnInit() {
    this.translationService.languageChanged.subscribe(() => {
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.apiService.getTypes().subscribe((types) => {
      this.types = types;
    });
    this.apiService.getMaps().subscribe((maps) => {
      this.maps = maps;
    });
    this.activatedRoute.paramMap.subscribe((p) => {
      this.routeId = +p.get("routeId");
      this.loadData();
    });
  }
  private loadData() {
    this.apiService.getRoute(this.routeId).subscribe((data) => {
      this.route = data;
      if (!this.route.firstDateTime) {
        this.route.firstDateTime = moment();
      }
      this.colour = this.route.overrideColour;
      this.selectedMaps = this.route.routeMaps.map((r) => r.mapId);
      this.form.patchValue(this.route);
      if (this.route.routeInstancesCount > 1) {
        this.form.controls.firstDateTime.disable();
      }
    });
  }

  onSubmit(values, goToInstances: boolean) {
    if (this.form.invalid) {
      return;
    }
    const mapsSelection = this.mapsSelection();
    if (mapsSelection.selectedOptions.selected.length === 0) {
      return false;
    }
    const route = values as UpdateRoute;
    route.routeId = this.route.routeId;
    route.overrideColour = this.colour;
    route.maps = mapsSelection.selectedOptions.selected.map(
      (s) => s.value
    );
    if (this.activeOperators()) {
      route.operatorIds = this.activeOperators();
    }
    this.apiService.updateRoute(values as Route).subscribe((_) => {
      if (!goToInstances) {
        this.goBack();
      } else {
        this.router.navigate([
          "/",
          "admin",
          "routes",
          "instances",
          this.route.routeId,
        ]);
      }
    });
  }

  goBack(): void {
    this.router.navigate(["/", "admin", "routes"]);
  }
  delete() {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      width: this.getWidth(),
      data: {
        item:
          this.translateService.instant("ROUTE.DELETEFRONT") +
          " " +
          this.route.name +
          " " +
          this.translateService.instant("ROUTE.DELETEBACK"),
      },
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.apiService.deleteRoute(this.route.routeId).subscribe((_) => {
          this.goBack();
        });
      }
    });
  }

  get countriesString() {
    if (!this.countriesSelection() || !this.countries) {
      return "";
    }
    const countries = this.countries
      .filter((c) =>
        this.countriesSelection().selectedOptions.selected.some(
          (rc) => rc.value === c.countryId
        )
      )
      .map((c) => this.name(c));
    if (countries.length > 3) {
      return (
        "" +
        countries.length +
        " " +
        this.translateService.instant("ROUTEDETAILS.COUNTRIESINSTRING")
      );
    }
    return countries.join(", ");
  }

  get mapsString() {
    if (!this.mapsSelection() || !this.maps) {
      return "";
    }
    const maps = this.maps
      .filter((m) =>
        this.mapsSelection().selectedOptions.selected.some(
          (rm) => rm.value === m.mapId
        )
      )
      .map((m) => this.name(m));
    if (maps.length > 3) {
      return (
        "" +
        maps.length +
        " " +
        this.translateService.instant("ROUTEDETAILS.MAPSINSTRING")
      );
    }
    return maps.join(", ");
  }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }

  export() {
    this.apiService.getExport(this.route.routeId).subscribe((data) => {
      saveAs(data, this.route.name.trim().replace(" ", "_") + ".gpx");
    });
  }

  private getWidth() {
    let width = "90%";
    if (window.innerWidth > 600) {
      width = "50%";
    }
    return width;
  }

  get isAdmin() {
    return this.authService.isLoggedIn && this.authService?.admin;
  }

  assignRegions() {
    this.apiService.assignRegionsToRoute(this.route.routeId).subscribe({
      next: () => {
        this.loadData();
      },
    });
  }
}
