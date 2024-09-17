import { Component, OnInit, signal, ViewChild } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { Route } from "src/app/models/route.model";
import { ApiService } from "src/app/services/api.service";
import {
  UntypedFormBuilder,
  Validators,
  UntypedFormGroup,
} from "@angular/forms";
import * as moment from "moment";
import { RouteType } from "src/app/models/routeType.model";
import { Country } from "src/app/models/country.model";
import { MatSelectionList } from "@angular/material/list";
import { UpdateRoute } from "src/app/models/updateRoute.model";
import { Map } from "src/app/models/map.model";
import { TranslateService } from "@ngx-translate/core";
import { DateAdapter } from "@angular/material/core";
import { TranslationService } from "src/app/services/translation.service";
import { MatDialog } from "@angular/material/dialog";
import { AreYouSureDialogComponent } from "src/app/are-you-sure-dialog/are-you-sure-dialog.component";
import * as saveAs from "file-saver";
import { AuthenticationService } from "src/app/services/authentication.service";
import { OperatorService } from "src/app/services/operator.service";

@Component({
  selector: "app-route-detail",
  templateUrl: "./route-detail.component.html",
  styleUrls: ["./route-detail.component.scss"],
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

  @ViewChild("countriesSelection") countriesSelection: MatSelectionList;
  @ViewChild("mapsSelection") mapsSelection: MatSelectionList;

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
    private operatorService: OperatorService
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
    if (this.mapsSelection.selectedOptions.selected.length === 0) {
      return false;
    }
    const route = values as UpdateRoute;
    route.routeId = this.route.routeId;
    route.overrideColour = this.colour;
    route.maps = this.mapsSelection.selectedOptions.selected.map(
      (s) => s.value
    );
    if (!!this.activeOperators()) {
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
      if (!!result) {
        this.apiService.deleteRoute(this.route.routeId).subscribe((_) => {
          this.goBack();
        });
      }
    });
  }

  get countriesString() {
    if (!this.countriesSelection || !this.countries) {
      return "";
    }
    const countries = this.countries
      .filter((c) =>
        this.countriesSelection.selectedOptions.selected.some(
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
    if (!this.mapsSelection || !this.maps) {
      return "";
    }
    const maps = this.maps
      .filter((m) =>
        this.mapsSelection.selectedOptions.selected.some(
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
      saveAs(data, this.route.name.trim().replace(" ", "_") + ".kml");
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
