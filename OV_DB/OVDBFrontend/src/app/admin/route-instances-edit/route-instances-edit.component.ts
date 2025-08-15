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
import { MatSlideToggle } from '@angular/material/slide-toggle';
import moment, { Moment } from 'moment';

@Component({
  selector: 'app-route-instances-edit',
  templateUrl: './route-instances-edit.component.html',
  styleUrls: ['./route-instances-edit.component.scss'],
  imports: [MatDialogTitle, CdkScrollable, MatDialogContent, MatCard, MatCardContent, MatSlideToggle, MatFormField, MatLabel, MatInput, MatDatepickerInput, FormsModule, MatDatepickerToggle, MatSuffix, MatDatepicker, MatIcon, MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatAutocompleteTrigger, MatAutocomplete, MatOption, MatFooterCellDef, MatFooterCell, MatIconButton, MatCheckbox, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow, MatFooterRowDef, MatFooterRow, MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatSelectionList, MatListOption, MatDialogActions, MatButton, AsyncPipe, TranslateModule]
})
export class RouteInstancesEditComponent implements OnInit {
  readonly table = viewChild<MatTable<RouteInstanceProperty>>('table');
  instance: RouteInstance;
  new = false;
  options = [];
  filteredOptions: BehaviorSubject<string[]> = new BehaviorSubject<string[]>(this.options);
  maps: Map[];
  selectedMaps: number[] = [];
  useDetailedTime = false;

  // Getter and setter for datetime-local inputs (legacy - keeping for compatibility)
  get startTimeLocal(): string {
    if (!this.instance.startTime) return '';
    const date = new Date(this.instance.startTime);
    // Use local time representation to avoid timezone shifts
    return this.formatDateTimeLocal(date);
  }

  set startTimeLocal(value: string) {
    if (!value) {
      this.instance.startTime = undefined;
      return;
    }
    // Parse as local time to prevent timezone conversion issues
    this.instance.startTime = this.formatDateTimeLocal(new Date(value));
  }

  get endTimeLocal(): string {
    if (!this.instance.endTime) return '';
    const date = new Date(this.instance.endTime);
    // Use local time representation to avoid timezone shifts  
    return this.formatDateTimeLocal(date);
  }

  set endTimeLocal(value: string) {
    if (!value) {
      this.instance.endTime = undefined;
      return;
    }
    // Parse as local time to prevent timezone conversion issues
    this.instance.endTime = this.formatDateTimeLocal(new Date(value));
  }

  // Separate time inputs for better UX
  get startTimeOnly(): string {
    if (!this.instance.startTime) return '';
    const date = new Date(this.instance.startTime);
    return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
  }

  set startTimeOnly(value: string) {
    if (!value) {
      this.instance.startTime = undefined;
      return;
    }
    console.log(value);

    const baseDate = this.instance.date ? new Date(this.instance.date) : new Date();
    const [hours, minutes] = value.split(':').map(num => parseInt(num, 10));
    const combinedDate = new Date(baseDate.getFullYear(), baseDate.getMonth(), baseDate.getDate(), hours, minutes);
    this.instance.startTime = this.formatDateTimeLocal(combinedDate);
    console.log(combinedDate);
  }

  get endTimeOnly(): string {
    if (!this.instance.endTime) return '';
    const date = new Date(this.instance.endTime);
    return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
  }

  set endTimeOnly(value: string) {
    if (!value) {
      this.instance.endTime = undefined;
      return;
    }

    const baseDate = this.instance.date ? new Date(this.instance.date) : new Date();
    const [hours, minutes] = value.split(':').map(num => parseInt(num, 10));
    const combinedDate = new Date(baseDate.getFullYear(), baseDate.getMonth(), baseDate.getDate(), hours, minutes);
    this.instance.endTime = this.formatDateTimeLocal(combinedDate);
  }

  private formatDateTimeLocal(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
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
    }
  }

  ngOnInit() {
    // Check if the instance already has time information
    this.useDetailedTime = !!(this.instance.startTime || this.instance.endTime);

    this.apiService.getAutocompleteForTags().subscribe(data => {
      this.options = data;
      this.updateSuggestions('');
    });
    this.apiService.getMaps().subscribe(data => {
      this.maps = data;
    })
  }

  onTimeModeChange() {
    if (!this.useDetailedTime) {
      // Switching to date-only mode, clear time information
      this.instance.startTime = undefined;
      this.instance.endTime = undefined;
    }
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
    if (this.instance.date['_isAMomentObject']) {
      this.instance.date = (this.instance.date as unknown as Moment).format('YYYY-MM-DD');
    }
    console.log(this.instance.date);
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
