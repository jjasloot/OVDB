import { Component, DestroyRef, OnInit, AfterViewInit, ElementRef, HostListener, viewChild, inject, computed } from "@angular/core";
import { ApiService } from "src/app/services/api.service";
import { Router } from "@angular/router";
import { MatPaginator } from "@angular/material/paginator";
import { RouteInstancesDataSource } from "../data-sources/route-instances-data-source";
import { AuthenticationService } from "src/app/services/authentication.service";
import {
  tap,
  debounceTime,
  distinctUntilChanged,
  switchMap,
} from "rxjs/operators";
import { merge, fromEvent, Subject } from "rxjs";
import { MatSort, MatSortHeader } from "@angular/material/sort";
import { saveAs } from "file-saver";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { TranslationService } from "src/app/services/translation.service";
import { MatCheckboxChange, MatCheckbox } from "@angular/material/checkbox";
import { MatDialog } from "@angular/material/dialog";
import { RouteInstancesEditComponent } from "../route-instances-edit/route-instances-edit.component";
import { OperatorService } from "src/app/services/operator.service";
import { TableStateService } from "src/app/services/table-state.service";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatFormField } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { MatButton, MatIconButton } from "@angular/material/button";
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow } from "@angular/material/table";
import { MatChipListbox, MatChipOption } from "@angular/material/chips";
import { MatTooltip } from "@angular/material/tooltip";
import { MatIcon } from "@angular/material/icon";
import { AsyncPipe, DatePipe, DecimalPipe } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { RoutesListBottomsheetComponent } from "../routes-list/routes-list-bottomsheet/routes-list-bottomsheet.component";
import { RoutesListActions } from "src/app/models/routes-list-actions.enum";
import { RouteInstanceListDTO } from "src/app/models/routeInstanceList.model";
import { RouteInstance } from "src/app/models/routeInstance.model";
import { MatBottomSheet } from "@angular/material/bottom-sheet";
import { TableState } from "src/app/models/table-state.model";
import { UserPreferenceService } from "src/app/services/user-preference.service";

@Component({
  selector: "app-route-instances-list",
  templateUrl: "./route-instances-list.component.html",
  styleUrls: ["./route-instances-list.component.scss"],
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
    DecimalPipe,
    TranslateModule,
  ]
})
export class RouteInstancesListComponent implements OnInit, AfterViewInit {
  private apiService = inject(ApiService);
  private router = inject(Router);
  private translationService = inject(TranslationService);
  private operatorService = inject(OperatorService);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);
  private bottomSheet = inject(MatBottomSheet);
  private tableStateService = inject(TableStateService);
  private userPreferenceService = inject(UserPreferenceService);
  private readonly TABLE_ID = 'route-instances-list';

  instancesDataSource: RouteInstancesDataSource;
  selectedInstanceIds: number[] = [];
  displayedColumns: string[] = ["select", "date", "time", "delay", "name", "type", "from", "to", "distance", "edit"];

  readonly paginator = viewChild(MatPaginator);
  readonly sort = viewChild(MatSort);
  readonly input = viewChild<ElementRef>("input");
  count: number;
  loading: boolean = true;
  filter$ = new Subject<void>();

  hasTrainlogExport = computed(() => this.userPreferenceService.enableTrainlogExport());
  exportingTrainlog = false;

  ngOnInit() {
    this.instancesDataSource = new RouteInstancesDataSource(this.apiService);
    this.currentLocale = this.translationService.dateLocale;
    // Restore saved table state
    this.restoreTableState();

    // Load instances with restored state or defaults
    const pageIndex = this.restoredState?.pageIndex ?? 0;
    const pageSize = this.restoredState?.pageSize ?? 10;
    const sortActive = this.restoredState?.sortActive ?? 'date';
    const sortDirection = this.restoredState?.sortDirection ?? 'desc';
    const filter = this.restoredState?.filter ?? '';

    this.instancesDataSource.loadInstances(
      pageIndex * pageSize,
      pageSize,
      sortActive,
      sortDirection === 'desc',
      filter
    ).subscribe((data) => {
      this.count = data.count;
      this.loading = false;
      this.hasInitialized = true;
    });
  }
  private hasInitialized = false;
  private restoredState: TableState | null = null;

  private restoreTableState(): void {
    const savedState = this.tableStateService.getTableState(this.TABLE_ID, {
      defaultPageSize: 10,
      defaultSortActive: 'date',
      defaultSortDirection: 'desc'
    });

    this.restoredState = savedState;
  }


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


  ngAfterViewInit() {
    this.applyRestoredState();
    this.sort().sortChange.subscribe(() => { this.paginator().pageIndex = 0; this.saveCurrentTableState(); });

    merge(this.sort().sortChange, this.paginator().page, this.filter$)
      .pipe(
        tap(() => {
          this.loading = true;
          if (this.hasInitialized) {
            this.saveCurrentTableState();
          }
        }),
        takeUntilDestroyed(this.destroyRef),
        switchMap(() => this.loadInstancesPage())
      )
      .subscribe({
        next: (routes) => {
          this.count = routes.count;
          this.hasInitialized = true;
        },
      });

    fromEvent(this.input().nativeElement, "keyup")
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        debounceTime(150),
        distinctUntilChanged(),
        tap(() => {
          this.paginator().pageIndex = 0;
          this.filter$.next();
        })
      )
      .subscribe();
  }

  loadInstancesPage() {
    return this.instancesDataSource.loadInstances(
      this.paginator().pageIndex * this.paginator().pageSize,
      this.paginator().pageSize,
      this.sort().active,
      this.sort().direction === "desc",
      this.filterValue
    );
  }

  get filterValue() {
    return this.input().nativeElement.value;
  }

  toggleInstance(element: RouteInstanceListDTO, event: MatCheckboxChange) {
    if (event.checked) {
      this.selectedInstanceIds.push(element.routeInstanceId);
    } else {
      this.selectedInstanceIds = this.selectedInstanceIds.filter(
        (id) => id !== element.routeInstanceId
      );
    }
  }

  isInstanceChecked(element: RouteInstanceListDTO): boolean {
    return this.selectedInstanceIds.includes(element.routeInstanceId);
  }

  clearSelection() {
    this.selectedInstanceIds = [];
  }

  exportToTrainlog() {
    if (this.selectedInstanceIds.length > 0) {
      this.exportingTrainlog = true;
      this.apiService.exportInstancesToTrainlog(this.selectedInstanceIds).subscribe({
        next: (data) => {
          saveAs(data, "trainlog_export.csv");
          this.exportingTrainlog = false;
        },
        error: () => {
          this.exportingTrainlog = false;
        }
      });
    }
  }

  view(element: RouteInstanceListDTO) {
    this.router.navigate(["/route", element.routeId, element.share]);
  }

  edit(element: RouteInstanceListDTO) {
    const dialogRef = this.dialog.open(RouteInstancesEditComponent, {
      width: '90%',
      data: { instance: element, new: false }
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      if (result) {
        this.apiService.updateRouteInstance(result).pipe(
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => {
          this.loadInstancesPage().subscribe();
          this.saveCurrentTableState();
        });
      }
    });
  }

  getArrivalDelay(element: RouteInstanceListDTO): number | null {
    return element.arrivalDelayMinutes ?? null;
  }

  formatDelay(minutes: number | null): string {
    if (minutes === null) return null;
    const rounded = Math.round(minutes);
    if (rounded === 0) return '+0 min';
    return rounded > 0 ? `+${rounded} min` : `${rounded} min`;
  }

  delayClass(minutes: number | null): string {
    if (minutes === null) return '';
    const rounded = Math.round(minutes);
    if (Math.abs(rounded) <= 1) return 'delay-ontime';
    return rounded > 0 ? 'delay-late' : 'delay-early';
  }

  getOperatorLogo(operatorId: number) {
    return this.operatorService.getOperatorLogo(operatorId);
  }

  name(routeType: any) {
    return this.translationService.getNameForItem(routeType);
  }

  openBottomSheet(instance: RouteInstanceListDTO): void {
    const ref = this.bottomSheet.open(RoutesListBottomsheetComponent);
    ref.afterDismissed().subscribe((action: RoutesListActions) => {
      switch (action) {
        case RoutesListActions.View:
          this.view(instance);
          return;
        case RoutesListActions.Edit:
          this.edit(instance);
          return;
      }
    });
  }

  currentLocale: string;
}
