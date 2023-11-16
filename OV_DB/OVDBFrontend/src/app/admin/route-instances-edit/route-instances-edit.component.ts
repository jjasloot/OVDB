import { Component, OnInit, Inject, ViewChild } from '@angular/core';
import { MatLegacyDialogRef as MatDialogRef, MAT_LEGACY_DIALOG_DATA as MAT_DIALOG_DATA } from '@angular/material/legacy-dialog';
import { ApiService } from 'src/app/services/api.service';
import { RouteInstance } from 'src/app/models/routeInstance.model';
import { RouteInstanceProperty } from 'src/app/models/routeInstanceProperty.model';
import { MatLegacyTable as MatTable } from '@angular/material/legacy-table';
import { BehaviorSubject } from 'rxjs';
import { Map } from '../../models/map.model'
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-route-instances-edit',
  templateUrl: './route-instances-edit.component.html',
  styleUrls: ['./route-instances-edit.component.scss']
})
export class RouteInstancesEditComponent implements OnInit {
  @ViewChild('table') table: MatTable<RouteInstanceProperty>;
  instance: RouteInstance;
  new = false;
  options = [];
  filteredOptions: BehaviorSubject<string[]> = new BehaviorSubject<string[]>(this.options);
  maps: Map[];
  selectedMaps: number[] = [];
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
    this.table.renderRows();
  }

  get canAddNewRow() {
    return !this.instance.routeInstanceProperties.every(p => !!p.key);
  }

  removeRow(index: number) {
    this.instance.routeInstanceProperties.splice(index, 1);
    this.table.renderRows();
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
