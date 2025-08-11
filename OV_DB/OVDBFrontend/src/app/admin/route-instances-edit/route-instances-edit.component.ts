import { Component, OnInit, Inject, viewChild } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { ApiService } from 'src/app/services/api.service';
import { RouteInstance } from 'src/app/models/routeInstance.model';
import { RouteInstanceProperty } from 'src/app/models/routeInstanceProperty.model';
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatFooterCellDef, MatFooterCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow, MatFooterRowDef, MatFooterRow } from '@angular/material/table';
import { BehaviorSubject } from 'rxjs';
import { Map } from '../../models/map.model'
import { TranslationService } from 'src/app/services/translation.service';
import { CdkScrollable } from '@angular/cdk/scrolling';
import { MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatDatepickerInput, MatDatepickerToggle, MatDatepicker } from '@angular/material/datepicker';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteTrigger, MatAutocomplete } from '@angular/material/autocomplete';
import { MatOption } from '@angular/material/core';
import { MatIconButton, MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle } from '@angular/material/expansion';
import { MatSelectionList, MatListOption } from '@angular/material/list';
import { AsyncPipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { MatCard, MatCardContent } from '@angular/material/card';

@Component({
    selector: 'app-route-instances-edit',
    templateUrl: './route-instances-edit.component.html',
    styleUrls: ['./route-instances-edit.component.scss'],
    imports: [MatDialogTitle, CdkScrollable, MatDialogContent, MatFormField, MatLabel, MatInput, MatDatepickerInput, FormsModule, MatDatepickerToggle, MatSuffix, MatDatepicker, MatCard, MatCardContent, MatIcon, MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatAutocompleteTrigger, MatAutocomplete, MatOption, MatFooterCellDef, MatFooterCell, MatIconButton, MatCheckbox, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow, MatFooterRowDef, MatFooterRow, MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatSelectionList, MatListOption, MatDialogActions, MatButton, AsyncPipe, TranslateModule]
})
export class RouteInstancesEditComponent implements OnInit {
  readonly table = viewChild<MatTable<RouteInstanceProperty>>('table');
  instance: RouteInstance;
  new = false;
  options = [];
  filteredOptions: BehaviorSubject<string[]> = new BehaviorSubject<string[]>(this.options);
  maps: Map[];
  selectedMaps: number[] = [];
  routeDistance: number | null = null;
  
  // Getter and setter for datetime-local inputs
  get startTimeLocal(): string {
    if (!this.instance.startTime) return '';
    const date = new Date(this.instance.startTime);
    return this.formatDateTimeLocal(date);
  }
  
  set startTimeLocal(value: string) {
    this.instance.startTime = value ? new Date(value) : undefined;
  }
  
  get endTimeLocal(): string {
    if (!this.instance.endTime) return '';
    const date = new Date(this.instance.endTime);
    return this.formatDateTimeLocal(date);
  }
  
  set endTimeLocal(value: string) {
    this.instance.endTime = value ? new Date(value) : undefined;
  }

  // Trip information getters
  get showTripInfo(): boolean {
    return this.instance.startTime && this.instance.endTime && this.routeDistance !== null;
  }

  get calculatedDuration(): number | null {
    if (!this.instance.startTime || !this.instance.endTime) return null;
    const start = new Date(this.instance.startTime);
    const end = new Date(this.instance.endTime);
    return (end.getTime() - start.getTime()) / (1000 * 60 * 60); // Hours
  }

  get estimatedSpeed(): number | null {
    const duration = this.calculatedDuration;
    if (!duration || duration <= 0 || this.routeDistance === null || this.routeDistance <= 0) return null;
    return this.routeDistance / duration;
  }

  private formatDateTimeLocal(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  formatDuration(durationHours: number): string {
    const hours = Math.floor(durationHours);
    const minutes = Math.round((durationHours - hours) * 60);
    if (hours === 0) {
      return `${minutes}m`;
    } else if (minutes === 0) {
      return `${hours}h`;
    } else {
      return `${hours}h ${minutes}m`;
    }
  }
  constructor(
    public dialogRef: MatDialogRef<RouteInstancesEditComponent>,
    private translationService: TranslationService,
    private apiService: ApiService,
    @Inject(MAT_DIALOG_DATA) data) {
    if (!!data && data.instance) {
      this.instance = Object.assign({}, data.instance);
      this.instance.routeInstanceProperties = Object.assign([], data.instance.routeInstanceProperties);
      this.instance.routeInstanceProperties.push({} as RouteInstanceProperty);
      if (data.new) {
        this.new = true;
      }
      this.selectedMaps = this.instance.routeInstanceMaps.map(rim => rim.mapId);
      
      // Get route information for distance calculation
      if (data.route) {
        this.routeDistance = data.route.overrideDistance || data.route.calculatedDistance;
      }
    }
  }

  ngOnInit() {
    this.apiService.getAutocompleteForTags().subscribe(data => {
      this.options = data;
      this.updateSuggestions('');
    });
    this.apiService.getMaps().subscribe(data => {
      this.maps = data;
    })
  }
  updateSuggestions(value: string) {
    const filterValue = value.toLowerCase();
    const filterBasedOnExisting = this.options.filter(option => !this.instance.routeInstanceProperties.some(x => x.key === option));

    const filterBasedOnTyping = filterBasedOnExisting.filter(option => option.toLowerCase().indexOf(filterValue) === 0);
    this.filteredOptions.next(filterBasedOnTyping);
  }

  cancel() {
    this.dialogRef.close();
  }

  return() {
    if (this.incomplete) {
      return;
    }
    if (!this.instance.routeInstanceProperties[this.instance.routeInstanceProperties.length - 1].key) {
      this.instance.routeInstanceProperties =
        this.instance.routeInstanceProperties.slice(0, this.instance.routeInstanceProperties.length - 1);
    }
    this.instance.routeInstanceMaps = this.selectedMaps.map(s => { return { mapId: s } });
    this.dialogRef.close(this.instance);
  }

  disableValue(row: RouteInstanceProperty) {
    return (row.bool !== undefined && row.bool !== null);
  }
  disableBool(row: RouteInstanceProperty) {
    return !!row.value;
  }
  addRow() {
    this.instance.routeInstanceProperties.push({} as RouteInstanceProperty);
    this.table().renderRows();
  }

  get canAddNewRow() {
    return !this.instance.routeInstanceProperties.every(p => !!p.key);
  }

  removeRow(index: number) {
    this.instance.routeInstanceProperties.splice(index, 1);
    this.table().renderRows();
  }

  rowIsEmpty(prop: RouteInstanceProperty) {
    return !prop.key;
  }

  get incomplete() {
    return !this.instance.date;
  }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }
}
