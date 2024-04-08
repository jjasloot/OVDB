import { Route, RouteList } from "src/app/models/route.model";
import { DataSource } from "@angular/cdk/table";
import { BehaviorSubject, Observable, of } from "rxjs";
import { ApiService } from "src/app/services/api.service";
import { CollectionViewer } from "@angular/cdk/collections";
import { catchError, finalize, tap } from "rxjs/operators";

export class RoutesDataSource implements DataSource<Route> {
  private routesSubject = new BehaviorSubject<Route[]>([]);
  private loadingSubject = new BehaviorSubject<boolean>(false);

  public loading$ = this.loadingSubject.asObservable();

  constructor(private apiService: ApiService) {}

  connect(collectionViewer: CollectionViewer): Observable<Route[]> {
    return this.routesSubject.asObservable();
  }

  disconnect(collectionViewer: CollectionViewer): void {
    this.routesSubject.complete();
    this.loadingSubject.complete();
  }

  loadRoutes(
    start = 0,
    count = 10,
    column = "date",
    descending = true,
    filter = ""
  ): Observable<RouteList> {
    this.loadingSubject.next(true);

    return this.apiService
      .getAllRoutes(start, count, column, descending, filter)
      .pipe(
        catchError(() => of({ count: -1, routes: [] })),
        tap((routes) => {
          this.routesSubject.next(routes.routes);
          if (routes?.count >= 0) {
            this.loadingSubject.next(false);
          }
        })
      );
  }
}
