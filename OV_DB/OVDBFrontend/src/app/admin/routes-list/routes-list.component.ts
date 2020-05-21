import { Component, OnInit, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Route } from 'src/app/models/route.model';
import { Router } from '@angular/router';
import { DataUpdateService } from 'src/app/services/data-update.service';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { RoutesDataSource } from '../data-sources/routes-data-source';
import { tap, debounceTime, distinctUntilChanged, filter } from 'rxjs/operators';
import { merge, fromEvent } from 'rxjs'
import { MatSort } from '@angular/material/sort';
import { saveAs } from 'file-saver';
import { TranslateService } from '@ngx-translate/core';
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-routes-list',
  templateUrl: './routes-list.component.html',
  styleUrls: ['./routes-list.component.scss']
})
export class RoutesListComponent implements OnInit, AfterViewInit {
  routes: Route[];
  loading: boolean;
  displayedColumns: string[] = ['name', 'date', 'maps', 'type', 'edit'];
  dataSource: RoutesDataSource

    ;

  @ViewChild(MatPaginator, { static: true }) paginator: MatPaginator;
  @ViewChild(MatSort) sort: MatSort;
  @ViewChild('input') input: ElementRef;

  count: number;

  constructor(
    private apiService: ApiService,
    private router: Router,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private dataUpdateService: DataUpdateService) { }

  ngOnInit() {
    this.loadCount();
    this.dataSource = new RoutesDataSource(this.apiService);
    this.dataSource.loadRoutes();
    this.loading = true;


  }

  private loadCount() {
    this.apiService.getRouteCount(this.filterValue).subscribe(count => {
      this.count = count;
    });
  }

  edit(id: number) {
    this.router.navigate(['/admin', 'routes', id]);
  }

  export(route: Route) {
    this.apiService.getExport(route.routeId).subscribe(data => {
      saveAs(data, route.name.trim().replace(' ', '_') + '.kml')
    });
  }
  exportAll() {
    this.apiService.getCompleteExport().subscribe(data => {
      saveAs(data, 'Export.kml')
    });
  }
  get filterValue() {
    if (!this.input || !this.input.nativeElement) {
      return '';
    }
    return this.input.nativeElement.value;
  }

  ngAfterViewInit() {
    fromEvent(this.input.nativeElement, 'keyup')
      .pipe(
        debounceTime(150),
        distinctUntilChanged(),
        tap(() => {
          this.paginator.pageIndex = 0;
          this.loadRoutesPage();
          this.loadCount();
        })
      )
      .subscribe();

    this.sort.sortChange.subscribe(() => this.paginator.pageIndex = 0);

    merge(this.sort.sortChange, this.paginator.page)
      .pipe(
        tap(() => this.loadRoutesPage())
      )
      .subscribe();
  }

  loadRoutesPage() {
    this.dataSource.loadRoutes(
      this.paginator.pageIndex * this.paginator.pageSize,
      this.paginator.pageSize,
      this.sort.active,
      this.sort.direction === 'desc',
      this.filterValue);
  }

  getMapsString(element: Route) {
    if (element.routeMaps.length === 0) {
      return this.translateService.instant('ROUTESLIST.NOMAPS')
    }
    if (element.routeMaps.length > 3) {
      return element.routeMaps.length + ' ' + this.translateService.instant('ROUTESLIST.MAPSFORCOUNT');
    }
    return element.routeMaps.map(r => r.map.name).join(', ');
  }

  get currentLocale() {
    return this.translationService.dateLocale;
  }

  name(item) {
    return this.translationService.getNameForItem(item);
  }
}
