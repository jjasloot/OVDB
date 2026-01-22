import { Component, DestroyRef, OnInit, inject } from "@angular/core";
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

  routeId: number;
  route: Route;
  loading = false;
  fromTraewelling = false;
  trawellingTripData: TrawellingTripContext | null = null;
  newRoute = false;
  get instances() {
    if (!this.route) {
      return [];
    }
    return this.route.routeInstances;
  }

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
        this.routeId = +p.get("routeId");
        this.getData();
      });

    // Check if coming from Träwelling
    this.activatedRoute.queryParams
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.newRoute = !!params['newRoute'];
        if (params['traewellingTripId']) {
          this.fromTraewelling = true;
          const tripDataStr = sessionStorage.getItem('traewellingTripContext');
          if (tripDataStr) {
            const trawellingTripData = JSON.parse(tripDataStr) as TrawellingTripContext;
            if (trawellingTripData.tripId === +params['traewellingTripId']) {
              // If the IDs match, use the data
              this.trawellingTripData = trawellingTripData;
              if (!this.newRoute) {
                this.add();
              }
            }
          }
        }
      });
  }


  private getData() {
    this.loading = true;
    this.apiService.getRouteInstances(this.routeId).subscribe((data) => {
      this.route = data;
      this.loading = false;
      if (this.newRoute && this.trawellingTripData && this.route.routeInstances.length === 1) {
        this.prefillWithTraewellingData(this.route.routeInstances[0]);
        this.edit(this.route.routeInstances[0]);
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
        instance, route: this.route,
        traewellingTripData: this.fromTraewelling ? this.trawellingTripData : null // Pass trip context for display
      },
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
        this.router.navigate(['/admin/routes/instances', this.routeId]);
        this.apiService.updateRouteInstance(result).subscribe(() => {
          this.getData();
        });
      }
    });
  }

  get routeName() {
    if (!this.route) {
      return "";
    }
    return this.translationService.getNameForItem(this.route);
  }

  get typeName() {
    if (!this.route) {
      return "";
    }
    return this.translationService.getNameForItem(this.route.routeType);
  }
  add() {
    // Create new RouteInstance
    const newInstance = {
      routeId: this.routeId,
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
        route: this.route,
        traewellingTripData: this.fromTraewelling ? this.trawellingTripData : null // Pass trip context for display
      },
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
        this.router.navigate(['/admin/routes/instances', this.routeId]);
        this.apiService.updateRouteInstance(result).subscribe(() => {
          this.getData();
          // Clear the fromTraewelling flag after successful creation
          this.fromTraewelling = false;
          this.trawellingTripData = null;
        });
      }
    });
  }

  private prefillWithTraewellingData(newInstance: RouteInstance) {
    if (this.fromTraewelling && this.trawellingTripData) {
      newInstance.date = this.trawellingTripData.date ? new Date(this.trawellingTripData.date).toISOString().split('T')[0] : undefined;
      newInstance.startTime = this.trawellingTripData.departureTime;
      newInstance.endTime = this.trawellingTripData.arrivalTime;
      newInstance.traewellingStatusId = this.trawellingTripData.tripId;
      // Add tags as properties
      if (this.trawellingTripData.tags && this.trawellingTripData.tags.length > 0) {
        this.trawellingTripData.tags.forEach(tag => {
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
