import {
  Component,
  OnInit,
  ViewChild,
  AfterViewInit,
  ElementRef,
  HostListener,
} from "@angular/core";
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
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatFormField } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { MatButton, MatIconButton } from "@angular/material/button";
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow } from "@angular/material/table";
import { MatChipListbox, MatChipOption } from "@angular/material/chips";
import { MatTooltip } from "@angular/material/tooltip";
import { MatIcon } from "@angular/material/icon";
import { AsyncPipe, DatePipe } from "@angular/common";

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
  routes: Route[];
  loading: boolean;
  displayedColumns: string[] = [
    "select",
    "name",
    "date",
    "instances",
    "maps",
    "type",
    "edit",
  ];
  dataSource: RoutesDataSource;
  selectedRoutes: number[] = [];

  @ViewChild(MatPaginator, { static: true }) paginator: MatPaginator;
  @ViewChild(MatSort) sort: MatSort;
  @ViewChild("input") input: ElementRef;
  count: number;
  @HostListener("window:resize", ["$event"])
  onResize(event) {
    this.restrictColumnsOnWidth();
  }
  filter$ = new Subject<void>();

  constructor(
    private apiService: ApiService,
    private router: Router,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private dialog: MatDialog,
    private bottomSheet: MatBottomSheet,
    private operatorService: OperatorService
  ) {}

  ngOnInit() {
    this.dataSource = new RoutesDataSource(this.apiService);
    this.dataSource.loadRoutes();
    this.loading = true;
    this.restrictColumnsOnWidth();
  }

  restrictColumnsOnWidth() {
    if (window.innerWidth > 1200) {
      this.displayedColumns = [
        "select",
        "name",
        "operator",
        "date",
        "instances",
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
    if (!this.input || !this.input.nativeElement) {
      return "";
    }
    return this.input.nativeElement.value;
  }

  ngAfterViewInit() {
    fromEvent(this.input.nativeElement, "keyup")
      .pipe(
        debounceTime(150),
        distinctUntilChanged(),
        tap(() => {
          this.paginator.pageIndex = 0;
          this.filter$.next();
        })
      )
      .subscribe();

    this.sort.sortChange.subscribe(() => (this.paginator.pageIndex = 0));

    merge(this.sort.sortChange, this.paginator.page, this.filter$)
      .pipe(
        tap(() => {
          this.loading = true;
        }),
        switchMap(() => this.loadRoutesPage())
      )
      .subscribe({
        next: (routes) => {
          this.count = routes.count;
        },
      });

    this.filter$.next();
  }

  loadRoutesPage() {
    return this.dataSource.loadRoutes(
      this.paginator.pageIndex * this.paginator.pageSize,
      this.paginator.pageSize,
      this.sort.active,
      this.sort.direction === "desc",
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
    if (!!route.overrideDistance) {
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
}
