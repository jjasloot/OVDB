import { Component, DestroyRef, OnInit, signal, viewChild, inject, computed } from "@angular/core";
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
import { TrawellingTripContext } from "src/app/models/traewelling.model";
import { TrawellingContextCardComponent } from "src/app/traewelling/context-card/traewelling-context-card.component";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";

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
    TrawellingContextCardComponent
  ]
})
export class RouteDetailComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private formBuilder = inject(UntypedFormBuilder);
  private authService = inject(AuthenticationService);
  private dateAdapter = inject<DateAdapter<any>>(DateAdapter);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  routeId = signal<number>(0);
  route = signal<Route | null>(null);
  form: UntypedFormGroup;
  types = signal<RouteType[]>([]);
  countries = signal<Country[]>([]);
  maps = signal<Map[]>([]);
  activeOperators = signal<number[]>([]);
  logo = signal<string | null>(null);
  colour = signal<string>("");
  fromTraewelling = signal(false);
  trawellingTripData = signal<TrawellingTripContext | null>(null);

  readonly countriesSelection = viewChild<MatSelectionList>("countriesSelection");
  readonly mapsSelection = viewChild<MatSelectionList>("mapsSelection");

  selectedOptions: number[];
  selectedMaps: number[];

  constructor() {
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
    this.translationService.languageChanged
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.dateAdapter.setLocale(this.translationService.dateLocale);
      });
    this.apiService.getTypes()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((types) => {
        this.types.set(types);
      });
    this.apiService.getMaps()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((maps) => {
        this.maps.set(maps.filter(m => !m.completed));
      });
    this.activatedRoute.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((p) => {
        this.routeId.set(+p.get("routeId"));
        this.loadData();
      });

    // Check if coming from Träwelling
    this.activatedRoute.queryParams
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        if (params['traewellingTripId']) {
          this.fromTraewelling.set(true);
          const tripDataStr = sessionStorage.getItem('traewellingTripContext');
          if (tripDataStr) {
            const trawellingTripData = JSON.parse(tripDataStr) as TrawellingTripContext;
            if (trawellingTripData.tripId === +params['traewellingTripId']) {
              // If the IDs match, use the data
              this.trawellingTripData.set(trawellingTripData);
            }
          }
        }
      });
  }
  private loadData() {
    this.apiService.getRoute(this.routeId()).subscribe((data) => {
      this.route.set(data);
      if (!data.firstDateTime) {
        data.firstDateTime = moment();
      }
      this.colour.set(data.overrideColour);
      this.selectedMaps = data.routeMaps.map((r) => r.mapId);
      this.form.patchValue(data);
      if (data.routeInstancesCount > 1) {
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
    route.routeId = this.route()!.routeId;
    route.overrideColour = this.colour();
    route.maps = mapsSelection.selectedOptions.selected.map(
      (s) => s.value
    );
    if (this.activeOperators()) {
      route.operatorIds = this.activeOperators();
    }
    this.apiService.updateRoute({ ...values, firstDateTime: this.formatDateTimeLocal(new Date(values.firstDateTime)) } as Route).subscribe((_) => {
      if (!goToInstances) {
        this.goBack();
      } else {
        const navigationParams: any = {
          route: ["/", "admin", "routes", "instances", this.route()!.routeId]
        };

        // If we have Träwelling trip data, pass it through query params and session storage
        if (this.fromTraewelling() && this.trawellingTripData()) {
          navigationParams.queryParams = { traewellingTripId: this.trawellingTripData()!.tripId, newRoute: true };
        }

        this.router.navigate(navigationParams.route, navigationParams.queryParams ? { queryParams: navigationParams.queryParams } : {});
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
          this.route()!.name +
          " " +
          this.translateService.instant("ROUTE.DELETEBACK"),
      },
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.apiService.deleteRoute(this.route()!.routeId).subscribe((_) => {
          this.goBack();
        });
      }
    });
  }

  get countriesString() {
    if (!this.countriesSelection() || !this.countries) {
      return "";
    }
    const countries = this.countries()
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
    const maps = this.maps()
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
    this.apiService.getExport(this.route()!.routeId).subscribe((data) => {
      saveAs(data, this.route()!.name.trim().replace(" ", "_") + ".gpx");
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
    this.apiService.assignRegionsToRoute(this.route()!.routeId).subscribe({
      next: () => {
        this.loadData();
      },
    });
  }

  private formatDateTimeLocal(date: Date): string {
    if(!date || isNaN(date.getTime())) {
      return "";
    }
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

}
