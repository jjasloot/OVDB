import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from 'src/app/services/api.service';
import { RouteInstance } from 'src/app/models/routeInstance.model';
import { TranslationService } from 'src/app/services/translation.service';
import { RouteInstanceProperty } from 'src/app/models/routeInstanceProperty.model';
import { TranslateService } from '@ngx-translate/core';
import { DatePipe } from '@angular/common';
import { MatDialog } from '@angular/material/dialog';
import { RouteInstancesEditComponent } from '../route-instances-edit/route-instances-edit.component';

@Component({
  selector: 'app-route-instances',
  templateUrl: './route-instances.component.html',
  styleUrls: ['./route-instances.component.scss']
})
export class RouteInstancesComponent implements OnInit {
  routeId: number;
  instances: RouteInstance[];

  constructor(
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService,
    private translationService: TranslationService,
    private translateService: TranslateService,
    private datePipe: DatePipe,
    private dialog: MatDialog
  ) { }

  ngOnInit(): void {
    this.activatedRoute.paramMap.subscribe(p => {
      this.routeId = +p.get('routeId');
      this.getData();
    });
  }
  private getData() {
    this.apiService.getRouteInstances(this.routeId).subscribe(data => {
      this.instances = data;
    });
  }

  get currentLocale() {
    return this.translationService.dateLocale;
  }

  value(property: RouteInstanceProperty) {
    if (!!property.value) {
      return property.value;
    }
    if (property.bool !== null && property.bool !== undefined) {
      return property.bool ? this.translateService.instant('YES') : this.translateService.instant('NO');
    }
    if (!!property.date) {
      return this.datePipe.transform(property.date, 'mediumDate', null, this.currentLocale);
    }
    return 'EMPTY';
  }

  edit(instance: RouteInstance) {
    const dialogRef = this.dialog.open(RouteInstancesEditComponent, {
      width: '80%',
      data: { instance }
    });
    dialogRef.afterClosed().subscribe((result: RouteInstance) => {
      this.apiService.updateRouteInstance(result).subscribe(() => {
        this.getData();
      })
    });

  }

}
