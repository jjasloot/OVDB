import { Component, OnDestroy, OnInit } from "@angular/core";
import { ActivatedRoute } from "@angular/router";
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
  routeId: number;
  route: Route;
  loading = false;
  get instances() {
    if (!this.route) {
      return [];
    }
    return this.route.routeInstances;
  }
  constructor(
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService,
    private translationService: TranslationService,
    private translateService: TranslateService,
    private dateAdapter: DateAdapter<any>,
    private dialog: MatDialog,
  ) { }

  ngOnInit(): void {
    this.dateAdapter.setLocale(this.translationService.dateLocale);
    this.translationService.languageChanged.subscribe(() => {
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.activatedRoute.paramMap.subscribe((p) => {
      this.routeId = +p.get("routeId");
      this.getData();
    });
  }


  private getData() {
    this.loading = true;
    this.apiService.getRouteInstances(this.routeId).subscribe((data) => {
      this.route = data;
      this.loading = false;
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
      width: "100%",
      data: { instance, route: this.route },
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
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
    const dialogRef = this.dialog.open(RouteInstancesEditComponent, {
      width: "100%",
      data: {
        instance: {
          routeId: this.routeId,
          routeInstanceMaps: [],
          routeInstanceProperties: [],
        } as RouteInstance,
        new: true,
        route: this.route,
      },
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
        this.apiService.updateRouteInstance(result).subscribe(() => {
          this.getData();
        });
      }
    });
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
