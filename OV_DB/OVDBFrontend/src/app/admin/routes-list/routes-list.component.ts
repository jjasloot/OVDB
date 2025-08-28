import { Component, OnInit, AfterViewInit, ElementRef, HostListener, viewChild, inject } from "@angular/core";
import { ApiService } from "src/app/services/api.service";
import { Route } from "src/app/models/route.model";
import { Router } from "@angular/router";
import { MatPaginator } from "@angular/material/paginator";
import { RoutesDataSource } from "../data-sources/routes-data-source";
import {
  tap,
  debounceTime,
  distinctUntilChanged,
  switchMap,
  finalize,
} from "rxjs/operators";
import { merge, fromEvent, Subject, Observable } from "rxjs";
import { MatSort, MatSortHeader } from "@angular/material/sort";
import { saveAs } from "file-saver";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { TranslationService } from "src/app/services/translation.service";
import { MatCheckboxChange, MatCheckbox } from "@angular/material/checkbox";
import { MatDialog } from "@angular/material/dialog";
import { EditMultipleComponent } from "../edit-multiple/edit-multiple.component";
import { RoutesListBottomsheetComponent } from "./routes-list-bottomsheet/routes-list-bottomsheet.component";
import { RoutesListActions } from "src/app/models/routes-list-actions.enum";
import { MatBottomSheet } from "@angular/material/bottom-sheet";
import { OperatorService } from "src/app/services/operator.service";
import { TableStateService } from "src/app/services/table-state.service";
import { TableState } from "src/app/models/table-state.model";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatFormField } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { MatButton, MatIconButton } from "@angular/material/button";
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow } from "@angular/material/table";
import { MatChipListbox, MatChipOption } from "@angular/material/chips";
import { MatTooltip } from "@angular/material/tooltip";
import { MatIcon } from "@angular/material/icon";
import { AsyncPipe, DatePipe, DecimalPipe, formatNumber } from "@angular/common";

@Component({
  selector: "app-routes-list",
  templateUrl: "./routes-list.component.html",
  styleUrls: ["./routes-list.component.scss"],
  imports: [
    MatProgressSpinner,
    MatFormField,
    MatInput,
    MatButton,
    MatTable,
    MatSort,
    MatColumnDef,
    MatHeaderCellDef,
    MatHeaderCell,
    MatSortHeader,
    MatCellDef,
    MatCell,
    MatCheckbox,
    MatChipListbox,
    MatChipOption,
    MatTooltip,
    MatIconButton,
    MatIcon,
    MatHeaderRowDef,
    MatHeaderRow,
    MatRowDef,
    MatRow,
    MatPaginator,
    AsyncPipe,
    DatePipe,
    TranslateModule,
  ]
})
export class RoutesListComponent implements OnInit, AfterViewInit {
  private apiService = inject(ApiService);
  private router = inject(Router);
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private dialog = inject(MatDialog);
  private bottomSheet = inject(MatBottomSheet);
  private operatorService = inject(OperatorService);
  private tableStateService = inject(TableStateService);

  private readonly TABLE_ID = 'routes-list';
  
  routes: Route[];
  loading: boolean;
  displayedColumns: string[] = [
    "select",
    "name",
    "date",
    "instances",
    "speed",
    "maps",
    "type",
    "edit",
  ];
  dataSource: RoutesDataSource;
  selectedRoutes: number[] = [];

  readonly paginator = viewChild(MatPaginator);
  readonly sort = viewChild(MatSort);
  readonly input = viewChild<ElementRef>("input");
  count: number;
  @HostListener("window:resize", ["$event"])
  onResize(event) {
    this.restrictColumnsOnWidth();
  }
  filter$ = new Subject<void>();


  ngOnInit() {
    this.dataSource = new RoutesDataSource(this.apiService);
    this.loading = true;
    this.restrictColumnsOnWidth();
    
    // Restore saved table state
    this.restoreTableState();
    
    // Load routes with restored state
    this.dataSource.loadRoutes();
  }

  restrictColumnsOnWidth() {
    if (window.innerWidth > 1200) {
      this.displayedColumns = [
        "select",
        "name",
        "operator",
        "date",
        "instances",
        "speed",
        "maps",
        "type",
        "edit",
      ];
    } else {
      this.displayedColumns = ["select", "name", "type", "edit"];
    }
  }

  edit(id: number) {
    this.router.navigate(["/admin", "routes", id]);
  }

  exportAll() {
    this.apiService.getCompleteExport().subscribe((data) => {
      saveAs(data, "Export.kml");
    });
  }
  get filterValue() {
    const input = this.input();
    if (!input || !input.nativeElement) {
      return "";
    }
    return input.nativeElement.value;
  }

  ngAfterViewInit() {
    // Apply restored table state to components after they're initialized
    this.applyRestoredState();

    fromEvent(this.input().nativeElement, "keyup")
      .pipe(
        debounceTime(150),
        distinctUntilChanged(),
        tap(() => {
          this.paginator().pageIndex = 0;
          this.saveCurrentTableState();
          this.filter$.next();
        })
      )
      .subscribe();

    this.sort().sortChange.subscribe(() => {
      this.paginator().pageIndex = 0;
      this.saveCurrentTableState();
    });

    this.paginator().page.subscribe(() => {
      this.saveCurrentTableState();
    });

    merge(this.sort().sortChange, this.paginator().page, this.filter$)
      .pipe(
        tap(() => {
          this.loading = true;
          // Save state whenever table changes, but not on initial filter trigger
          if (this.hasInitialized) {
            this.saveCurrentTableState();
          }
        }),
        switchMap(() => this.loadRoutesPage())
      )
      .subscribe({
        next: (routes) => {
          this.count = routes.count;
          this.hasInitialized = true;
        },
      });

    this.filter$.next();
  }

  private hasInitialized = false;

  private restoreTableState(): void {
    const savedState = this.tableStateService.getTableState(this.TABLE_ID, {
      defaultPageSize: 10,
      defaultSortActive: 'date',
      defaultSortDirection: 'desc'
    });

    this.restoredState = savedState;
  }

  private restoredState: TableState | null = null;

  private applyRestoredState(): void {
    if (this.restoredState) {
      // Apply pagination state
      if (this.paginator()) {
        this.paginator().pageIndex = this.restoredState.pageIndex;
        this.paginator().pageSize = this.restoredState.pageSize;
      }

      // Apply sorting state
      if (this.sort()) {
        this.sort().active = this.restoredState.sortActive;
        this.sort().direction = this.restoredState.sortDirection;
      }

      // Apply filter state
      if (this.input() && this.restoredState.filter) {
        this.input().nativeElement.value = this.restoredState.filter;
      }

      this.restoredState = null; // Clear the restored state after applying
    }
  }

  private saveCurrentTableState(): void {
    if (!this.paginator() || !this.sort()) return;

    const currentState = this.tableStateService.getCurrentState(
      this.paginator().pageIndex,
      this.paginator().pageSize,
      this.sort().active,
      this.sort().direction,
      this.filterValue
    );

    this.tableStateService.saveTableState(this.TABLE_ID, currentState);
  }

  loadRoutesPage() {
    return this.dataSource.loadRoutes(
      this.paginator().pageIndex * this.paginator().pageSize,
      this.paginator().pageSize,
      this.sort().active,
      this.sort().direction === "desc",
      this.filterValue
    );
  }

  getMapsString(element: Route) {
    if (element.routeMaps.length === 0) {
      return this.translateService.instant("ROUTESLIST.NOMAPS");
    }
    if (element.routeMaps.length > 3) {
      return (
        element.routeMaps.length +
        " " +
        this.translateService.instant("ROUTESLIST.MAPSFORCOUNT")
      );
    }
    return element.routeMaps.map((r) => r.name).join(", ");
  }

  get currentLocale() {
    return this.translationService.dateLocale;
  }

  name(item) {
    return this.translationService.getNameForItem(item);
  }
  openBottomSheet(route: Route): void {
    const ref = this.bottomSheet.open(RoutesListBottomsheetComponent);
    ref.afterDismissed().subscribe((action: RoutesListActions) => {
      switch (action) {
        case RoutesListActions.View:
          this.view(route);
          return;
        case RoutesListActions.Instances:
          this.instances(route);
          return;
        case RoutesListActions.Edit:
          this.edit(route.routeId);
          return;
      }
    });
  }

  view(route: Route) {
    this.router.navigate(["/route", route.routeId, route.share]);
  }

  isChecked(route: Route) {
    return this.selectedRoutes.includes(route.routeId);
  }

  toggle(route: Route, event: MatCheckboxChange) {
    if (event.checked) {
      this.selectedRoutes.push(route.routeId);
    } else {
      this.selectedRoutes = this.selectedRoutes.filter(
        (r) => r !== route.routeId
      );
    }
  }

  editMultiple() {
    const dialog = this.dialog.open(EditMultipleComponent, {
      width: this.getWidth(),
      data: {
        selectedRoutes: this.selectedRoutes,
      },
    });
    dialog.afterClosed().subscribe(() => this.loadRoutesPage());
  }

  clearSelection() {
    this.selectedRoutes = [];
  }

  getDistance(route: Route) {
    if (route.overrideDistance) {
      return route.overrideDistance;
    }
    return route.calculatedDistance;
  }

  instances(route: Route) {
    this.router.navigate(["/", "admin", "routes", "instances", route.routeId]);
  }

  private getWidth() {
    let width = "90%";
    if (window.innerWidth > 600) {
      width = "80%";
    }
    return width;
  }

  getOperatorLogo(operatorId: number): Observable<string> {
    return this.operatorService.getOperatorLogo(operatorId);
  }

  exportSet() {
    this.apiService.getExportForSet(this.selectedRoutes).subscribe((data) => {
      saveAs(data, "Export.zip");
    });
  }

  getSpeedDisplay(route: Route): string {
    if (!route.minAverageSpeedKmh && !route.maxAverageSpeedKmh) {
      return "-";
    }

    const min = route.minAverageSpeedKmh;
    const max = route.maxAverageSpeedKmh;

    if (!min || !max) {
      const speed = min || max;
      if (!speed) return "-";
      const formattedSpeed = formatNumber(speed, this.currentLocale, '1.1-1') || speed.toFixed(1);
      return `${formattedSpeed} km/h`;
    }

    if (Math.abs(min - max) < 0.1) {
      // If min and max are essentially the same, show single value
      const formattedSpeed = formatNumber(min, this.currentLocale, '1.1-1') || min.toFixed(1);
      return `${formattedSpeed} km/h`;
    } else {
      // Show range
      const formattedMin = formatNumber(min, this.currentLocale, '1.1-1') || min.toFixed(1);
      const formattedMax = formatNumber(max, this.currentLocale, '1.1-1') || max.toFixed(1);
      return `${formattedMin} - ${formattedMax} km/h`;
    }
  }
}
