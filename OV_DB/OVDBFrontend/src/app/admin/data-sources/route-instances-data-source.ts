import { RouteInstanceListDTO, RouteInstanceListResponseDTO } from "src/app/models/routeInstanceList.model";
import { DataSource } from "@angular/cdk/table";
import { BehaviorSubject, Observable, of } from "rxjs";
import { ApiService } from "src/app/services/api.service";
import { CollectionViewer } from "@angular/cdk/collections";
import { catchError, finalize, tap } from "rxjs/operators";

export class RouteInstancesDataSource implements DataSource<RouteInstanceListDTO> {
  private instancesSubject = new BehaviorSubject<RouteInstanceListDTO[]>([]);
  private loadingSubject = new BehaviorSubject<boolean>(false);

  public loading$ = this.loadingSubject.asObservable();

  constructor(private apiService: ApiService) {}

  connect(collectionViewer: CollectionViewer): Observable<RouteInstanceListDTO[]> {
    return this.instancesSubject.asObservable();
  }

  disconnect(collectionViewer: CollectionViewer): void {
    this.instancesSubject.complete();
    this.loadingSubject.complete();
  }

  loadInstances(
    start = 0,
    count = 10,
    column = "date",
    descending = true,
    filter = ""
  ): Observable<RouteInstanceListResponseDTO> {
    this.loadingSubject.next(true);

    return this.apiService
      .getAllRouteInstances(start, count, column, descending, filter)
      .pipe(
        catchError(() => of({ count: -1, instances: [] })),
        tap((data) => {
          this.instancesSubject.next(data.instances);
          if (data?.count >= 0) {
            this.loadingSubject.next(false);
          }
        })
      );
  }
}
