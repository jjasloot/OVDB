import { Component, OnInit, ChangeDetectorRef, AfterViewInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { ApiService } from '../services/api.service';
import { RouteInstance } from '../models/routeInstance.model';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { RouteInstanceProperty } from '../models/routeInstanceProperty.model';
import { DatePipe, LowerCasePipe } from '@angular/common';
import { CdkScrollable } from '@angular/cdk/scrolling';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatExpansionPanelDescription } from '@angular/material/expansion';
import { MatList, MatListItem } from '@angular/material/list';
import { MatButton } from '@angular/material/button';

@Component({
    selector: 'app-map-instance-dialog',
    templateUrl: './map-instance-dialog.component.html',
    styleUrls: ['./map-instance-dialog.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [MatDialogTitle, CdkScrollable, MatDialogContent, MatProgressSpinner, MatExpansionPanel, MatExpansionPanelHeader, MatExpansionPanelTitle, MatExpansionPanelDescription, MatList, MatListItem, MatDialogActions, MatButton, LowerCasePipe, DatePipe, TranslateModule]
})
export class MapInstanceDialogComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private translateService = inject(TranslateService);
  private datePipe = inject(DatePipe);
  dialogRef = inject<MatDialogRef<MapInstanceDialogComponent>>(MatDialogRef);
  data = inject(MAT_DIALOG_DATA);

  id: number;
  limits: any[];
  instances: RouteInstance[] = [];
  loading = true;
  loadAll = false;
  mapGuid = '';
  constructor() {
    const data = this.data;

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
    if (property.value) {
      return property.value;
    }
    if (property.bool !== null && property.bool !== undefined) {
      return property.bool ? this.translateService.instant('YES') : this.translateService.instant('NO');
    }
    return '';
  }
  getData() {
    // No period filter on the map means there is nothing to limit to: show every trip.
    if (this.limits.length === 0) {
      this.loadAll = true;
    }
    const requests = this.loadAll
      ? [this.apiService.getRouteInstancesForMap(this.mapGuid, this.id)]
      : this.limits.map(l => this.apiService.getRouteInstancesForMap(
        this.mapGuid,
        this.id,
        l.start.toFormat('yyyy-MM-dd'),
        l.end.toFormat('yyyy-MM-dd')
      ));
    this.loading = true;
    // One failed period leaves the others shown; the global toast reports the failure.
    forkJoin(requests.map(r => r.pipe(catchError(() => of({ routeInstances: [] as RouteInstance[] })))))
      .pipe(finalize(() => this.loading = false))
      .subscribe(results => this.instances = results.flatMap(r => r.routeInstances));
  }


}
