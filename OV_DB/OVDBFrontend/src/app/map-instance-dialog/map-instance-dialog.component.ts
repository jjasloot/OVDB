import { Component, OnInit, Inject, ChangeDetectorRef, AfterViewInit } from '@angular/core';
import { MatLegacyDialogRef as MatDialogRef, MAT_LEGACY_DIALOG_DATA as MAT_DIALOG_DATA } from '@angular/material/legacy-dialog';
import { merge } from 'rxjs';
import { ApiService } from '../services/api.service';
import { RouteInstance } from '../models/routeInstance.model';
import { TranslateService } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { RouteInstanceProperty } from '../models/routeInstanceProperty.model';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-map-instance-dialog',
  templateUrl: './map-instance-dialog.component.html',
  styleUrls: ['./map-instance-dialog.component.scss']
})
export class MapInstanceDialogComponent implements OnInit {
  id: number;
  limits: any[];
  instances: RouteInstance[] = [];
  loading = true;
  loadAll = false;
  mapGuid: string = '';
  constructor(
    private apiService: ApiService,
    private translationService: TranslationService,
    private translateService: TranslateService,
    private datePipe: DatePipe,
    public dialogRef: MatDialogRef<MapInstanceDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data) {
    this.id = data.id;
    this.limits = data.limits;
    this.mapGuid = data.mapGuid;
  }
  get currentLocale() {
    return this.translationService.dateLocale;
  }

  ngOnInit() {
    this.getData();
  }

  setLoadAll() {
    this.loadAll = true;
    this.getData();
  }

  close() {
    this.dialogRef.close();
  }
  value(property: RouteInstanceProperty) {
    if (!!property.value) {
      return property.value;
    }
    if (property.bool !== null && property.bool !== undefined) {
      return property.bool ? this.translateService.instant('YES') : this.translateService.instant('NO');
    }
    return '';
  }
  getData() {
    if (this.loadAll) {
      this.apiService.getRouteInstancesForMap(this.mapGuid, this.id).subscribe(data => {
        this.instances = data.routeInstances;
      });
      return;
    }
    let completed = 0;
    merge(...this.limits.map(l => this.apiService.getRouteInstancesForMap(
      this.mapGuid,
      this.id,
      l.start.format('YYYY-MM-DD'),
      l.end.format('YYYY-MM-DD')
    ))).subscribe(data => {
      this.instances = this.instances.concat(...data.routeInstances);
      completed++;
      if (completed === this.limits.length) {
        this.loading = false;
      }
    }, err => {
      completed++;
      if (completed === this.limits.length) {
        this.loading = false;
      }
    });
  }


}
