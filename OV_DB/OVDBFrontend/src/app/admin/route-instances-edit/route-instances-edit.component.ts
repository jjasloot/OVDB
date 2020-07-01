import { Component, OnInit, Inject, ViewChild } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { ApiService } from 'src/app/services/api.service';
import { RouteInstance } from 'src/app/models/routeInstance.model';
import { RouteInstanceProperty } from 'src/app/models/routeInstanceProperty.model';
import { MatTable } from '@angular/material/table';
import { TranslationService } from 'src/app/services/translation.service';
import { DateAdapter } from '@angular/material/core';

@Component({
  selector: 'app-route-instances-edit',
  templateUrl: './route-instances-edit.component.html',
  styleUrls: ['./route-instances-edit.component.scss']
})
export class RouteInstancesEditComponent implements OnInit {
  @ViewChild('table') table: MatTable<RouteInstanceProperty>;
  instance: RouteInstance;
  new = false;

  constructor(
    public dialogRef: MatDialogRef<RouteInstancesEditComponent>,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private dateAdapter: DateAdapter<any>,
    private apiService: ApiService,
    @Inject(MAT_DIALOG_DATA) data) {
    if (!!data && data.instance) {
      this.instance = Object.assign({}, data.instance);
      this.instance.routeInstanceProperties = Object.assign([], data.instance.routeInstanceProperties);
      this.instance.routeInstanceProperties.push({} as RouteInstanceProperty);
      if (data.new) {
        this.new = true;
      }
    }
  }

  ngOnInit() {
    this.dateAdapter.setLocale(this.translationService.dateLocale)

  }

  cancel() {
    this.dialogRef.close();
  }

  return() {
    if (this.incomplete) {
      return;
    }
    if (!this.instance.routeInstanceProperties[this.instance.routeInstanceProperties.length - 1].key) {
      this.instance.routeInstanceProperties = this.instance.routeInstanceProperties.slice(0, this.instance.routeInstanceProperties.length - 1)
    }
    console.log(this.instance);
    this.dialogRef.close(this.instance);
  }

  disableValue(row: RouteInstanceProperty) {
    return !!row.date || (row.bool !== undefined && row.bool !== null);
  }
  disableDate(row: RouteInstanceProperty) {
    return !!row.value || (row.bool !== undefined && row.bool !== null);
  }
  disableBool(row: RouteInstanceProperty) {
    return !!row.value || !!row.date;
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
}
