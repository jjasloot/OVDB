import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from "@angular/core";
import { MatFormField, MatLabel } from "@angular/material/form-field";
import { MatSelect } from "@angular/material/select";
import { MatOption } from "@angular/material/core";
import { MatChip, MatChipRemove } from "@angular/material/chips";
import { MatIcon } from "@angular/material/icon";
import { TranslateModule } from "@ngx-translate/core";
import { ApiService, RouteListFilters } from "src/app/services/api.service";
import { TranslationService } from "src/app/services/translation.service";
import { RouteType } from "src/app/models/routeType.model";
import { Map as OvdbMap } from "src/app/models/map.model";

/**
 * Type, map and year selects plus a removable operator chip, shared by the routes and trips lists.
 * Renders inline (display: contents) so the fields sit in the host list's toolbar row.
 */
@Component({
  selector: "app-route-list-filters",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatFormField, MatLabel, MatSelect, MatOption, MatChip, MatChipRemove, MatIcon, TranslateModule],
  host: { style: "display: contents" },
  styles: `mat-form-field { width: 150px; }`,
  template: `
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ "ROUTESLIST.FILTER_TYPE" | translate }}</mat-label>
      <mat-select [value]="filters().typeId ?? null" (selectionChange)="filterChange.emit({ key: 'typeId', value: $event.value })">
        <mat-option [value]="null">{{ "ALL" | translate }}</mat-option>
        @for (type of types(); track type.typeId) {
        <mat-option [value]="type.typeId">{{ typeName(type) }}</mat-option>
        }
      </mat-select>
    </mat-form-field>
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ "ROUTESLIST.FILTER_MAP" | translate }}</mat-label>
      <mat-select [value]="filters().mapId ?? null" (selectionChange)="filterChange.emit({ key: 'mapId', value: $event.value })">
        <mat-option [value]="null">{{ "ALL" | translate }}</mat-option>
        @for (map of maps(); track map.mapId) {
        <mat-option [value]="map.mapId">{{ map.name }}</mat-option>
        }
      </mat-select>
    </mat-form-field>
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ "ROUTESLIST.FILTER_YEAR" | translate }}</mat-label>
      <mat-select [value]="filters().year ?? null" (selectionChange)="filterChange.emit({ key: 'year', value: $event.value })">
        <mat-option [value]="null">{{ "ALL" | translate }}</mat-option>
        @for (year of years; track year) {
        <mat-option [value]="year">{{ year }}</mat-option>
        }
      </mat-select>
    </mat-form-field>
    @if (showMissing()) {
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ "ROUTESLIST.FILTER_MISSING" | translate }}</mat-label>
      <mat-select [value]="filters().missing ?? null" (selectionChange)="filterChange.emit({ key: 'missing', value: $event.value })">
        <mat-option [value]="null">{{ "ALL" | translate }}</mat-option>
        <mat-option value="operator">{{ "ROUTESLIST.MISSING_OPERATOR" | translate }}</mat-option>
        <mat-option value="type">{{ "ROUTESLIST.MISSING_TYPE" | translate }}</mat-option>
        <mat-option value="trips">{{ "ROUTESLIST.MISSING_TRIPS" | translate }}</mat-option>
      </mat-select>
    </mat-form-field>
    }
    @if (filters().operatorId) {
    <mat-chip (removed)="filterChange.emit({ key: 'operatorId', value: null })">
      {{ operatorName() ?? ("ROUTESLIST.OPERATOR_FILTER" | translate) }}
      <button matChipRemove [attr.aria-label]="'CLEAR' | translate"><mat-icon>cancel</mat-icon></button>
    </mat-chip>
    }
  `,
})
export class RouteListFiltersComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);

  filters = input.required<RouteListFilters>();
  operatorName = input<string | null>(null);
  /** The "needs attention" select: routes list only, it has no meaning for trips. */
  showMissing = input(false);
  filterChange = output<{ key: keyof RouteListFilters; value: number | string | null }>();

  types = signal<RouteType[]>([]);
  maps = signal<OvdbMap[]>([]);
  readonly years = Array.from({ length: new Date().getFullYear() - 1999 }, (_, i) => new Date().getFullYear() - i);

  ngOnInit(): void {
    this.apiService.getTypes().subscribe((types) => this.types.set(types));
    this.apiService.getMaps().subscribe((maps) => this.maps.set(maps));
  }

  typeName(type: RouteType) {
    return this.translationService.getNameForItem(type);
  }
}
