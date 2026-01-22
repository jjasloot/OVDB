import { Component, DestroyRef, OnInit, inject, signal, computed } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { ApiService } from "src/app/services/api.service";
import { RouteInstance } from "src/app/models/routeInstance.model";
import { TranslationService } from "src/app/services/translation.service";
import { RouteInstanceProperty } from "src/app/models/routeInstanceProperty.model";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { DatePipe, DecimalPipe, formatNumber } from "@angular/common";
import { MatDialog } from "@angular/material/dialog";
import { RouteInstancesEditComponent } from "../route-instances-edit/route-instances-edit.component";
import { Route } from "src/app/models/route.model";
import { AreYouSureDialogComponent } from "src/app/are-you-sure-dialog/are-you-sure-dialog.component";
import { DateAdapter } from "@angular/material/core";
import { SignalRService } from "src/app/services/signal-r.service";
import { MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle } from "@angular/material/expansion";
import { MatList, MatListItem } from "@angular/material/list";
import { MatButton, MatFabButton } from "@angular/material/button";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatIcon } from "@angular/material/icon";
import { TrawellingTripContext } from "src/app/models/traewelling.model";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";

@Component({
  selector: "app-route-instances",
  templateUrl: "./route-instances.component.html",
  styleUrls: ["./route-instances.component.scss"],
  imports: [
    MatExpansionPanel,
    MatExpansionPanelHeader,
    MatExpansionPanelTitle,
    MatList,
    MatListItem,
    MatButton,
    MatProgressSpinner,
    MatFabButton,
    MatIcon,
    DatePipe,
    TranslateModule,
  ],
})
export class RouteInstancesComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private translateService = inject(TranslateService);
  private dateAdapter = inject<DateAdapter<any>>(DateAdapter);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  routeId = signal<number>(0);
  route = signal<Route | null>(null);
  loading = signal(false);
  fromTraewelling = signal(false);
  trawellingTripData = signal<TrawellingTripContext | null>(null);
  newRoute = signal(false);

  instances = computed(() => {
    const r = this.route();
    if (!r) {
      return [];
    }
    return r.routeInstances;
  });

  ngOnInit(): void {
    this.dateAdapter.setLocale(this.translationService.dateLocale);
    this.translationService.languageChanged
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.dateAdapter.setLocale(this.translationService.dateLocale);
      });

    this.activatedRoute.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((p) => {
        this.routeId.set(+p.get("routeId"));
        this.getData();
      });

    // Check if coming from Träwelling
    this.activatedRoute.queryParams
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.newRoute.set(!!params['newRoute']);
        if (params['traewellingTripId']) {
          this.fromTraewelling.set(true);
          const tripDataStr = sessionStorage.getItem('traewellingTripContext');
          if (tripDataStr) {
            const trawellingTripData = JSON.parse(tripDataStr) as TrawellingTripContext;
            if (trawellingTripData.tripId === +params['traewellingTripId']) {
              // If the IDs match, use the data
              this.trawellingTripData.set(trawellingTripData);
              if (!this.newRoute()) {
                this.add();
              }
            }
          }
        }
      });
  }


  private getData() {
    this.loading.set(true);
    this.apiService.getRouteInstances(this.routeId()).subscribe((data) => {
      this.route.set(data);
      this.loading.set(false);
      if (this.newRoute() && this.trawellingTripData() && data.routeInstances.length === 1) {
        this.prefillWithTraewellingData(data.routeInstances[0]);
        this.edit(data.routeInstances[0]);
      }
    });
  }

  get currentLocale() {
    return this.translationService.dateLocale;
  }

  value(property: RouteInstanceProperty) {
    if (property.value) {
      return property.value;
    }
    if (property.bool !== null && property.bool !== undefined) {
      return property.bool
        ? this.translateService.instant("YES")
        : this.translateService.instant("NO");
    }
    return "";
  }

  getAverageSpeed(instance: RouteInstance): string {
    if (!instance.averageSpeedKmh) {
      return "-";
    }

    const formattedSpeed = formatNumber(instance.averageSpeedKmh, this.currentLocale, '1.1-1') || instance.averageSpeedKmh.toFixed(1);
    return `${formattedSpeed} km/h`;
  }

  getTripDuration(instance: RouteInstance): string {
    if (!instance.durationHours || instance.durationHours <= 0) {
      return "-";
    }

    const hours = Math.floor(instance.durationHours);
    const minutes = Math.round((instance.durationHours - hours) * 60);

    if (hours > 0 && minutes > 0) {
      return `${hours}h ${minutes}m`;
    } else if (hours > 0) {
      return `${hours}h`;
    } else {
      return `${minutes}m`;
    }
  }

  edit(instance: RouteInstance) {
    const dialogRef = this.dialog.open(RouteInstancesEditComponent, {
      width: "80%",
      maxWidth: "600px",
      data: {
        instance, route: this.route(),
        traewellingTripData: this.fromTraewelling() ? this.trawellingTripData() : null // Pass trip context for display
      },
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
        this.router.navigate(['/admin/routes/instances', this.routeId()]);
        this.apiService.updateRouteInstance(result).subscribe(() => {
          this.getData();
        });
      }
    });
  }

  get routeName() {
    const r = this.route();
    if (!r) {
      return "";
    }
    return this.translationService.getNameForItem(r);
  }

  get typeName() {
    const r = this.route();
    if (!r) {
      return "";
    }
    return this.translationService.getNameForItem(r.routeType);
  }
  add() {
    // Create new RouteInstance
    const newInstance = {
      routeId: this.routeId(),
      routeInstanceMaps: [],
      routeInstanceProperties: [],
    } as RouteInstance;

    // Pre-populate with Träwelling data if available
    this.prefillWithTraewellingData(newInstance);

    const dialogRef = this.dialog.open(RouteInstancesEditComponent, {
      width: "80%",
      maxWidth: "600px",
      data: {
        instance: newInstance,
        new: true,
        route: this.route(),
        traewellingTripData: this.fromTraewelling() ? this.trawellingTripData() : null // Pass trip context for display
      },
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
        this.router.navigate(['/admin/routes/instances', this.routeId()]);
        this.apiService.updateRouteInstance(result).subscribe(() => {
          this.getData();
          // Clear the fromTraewelling flag after successful creation
          this.fromTraewelling.set(false);
          this.trawellingTripData.set(null);
        });
      }
    });
  }

  private prefillWithTraewellingData(newInstance: RouteInstance) {
    if (this.fromTraewelling() && this.trawellingTripData()) {
      const tripData = this.trawellingTripData();
      newInstance.date = tripData!.date ? new Date(tripData!.date).toISOString().split('T')[0] : undefined;
      newInstance.startTime = tripData!.departureTime;
      newInstance.endTime = tripData!.arrivalTime;
      newInstance.traewellingStatusId = tripData!.tripId;
      // Add tags as properties
      if (tripData!.tags && tripData!.tags.length > 0) {
        tripData!.tags.forEach(tag => {
          newInstance.routeInstanceProperties.push({
            key: tag.key,
            value: tag.value,
            bool: null
          } as RouteInstanceProperty);
        });
      }
    }
  }

  delete(instance: RouteInstance) {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      width: "50%",
      data: { item: "deze rit wilt verwijderen" },
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
        this.apiService
          .deleteRouteInstance(instance.routeInstanceId)
          .subscribe(() => {
            this.getData();
          });
      }
    });
  }
}
