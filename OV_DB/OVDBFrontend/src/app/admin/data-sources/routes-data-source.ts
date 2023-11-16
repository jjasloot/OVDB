import { Route } from 'src/app/models/route.model';
import { DataSource } from '@angular/cdk/table';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { ApiService } from 'src/app/services/api.service';
import { CollectionViewer } from '@angular/cdk/collections';
import { catchError, finalize } from 'rxjs/operators';

export class RoutesDataSource implements DataSource<Route> {

    private routesSubject = new BehaviorSubject<Route[]>([]);
    private loadingSubject = new BehaviorSubject<boolean>(false);

    public loading$ = this.loadingSubject.asObservable();

    constructor(private apiService: ApiService) { }

    connect(collectionViewer: CollectionViewer): Observable<Route[]> {
        return this.routesSubject.asObservable();
    }

    disconnect(collectionViewer: CollectionViewer): void {
        this.routesSubject.complete();
        this.loadingSubject.complete();
    }

    loadRoutes(start = 0, count = 10, column = 'date', descending = true, filter = '') {

        this.loadingSubject.next(true);

        this.apiService.getAllRoutes(start, count, column, descending, filter).pipe(
            catchError(() => of([])),
            finalize(() => this.loadingSubject.next(false))
        )
            .subscribe(routes => {
              console.log(routes);
                this.routesSubject.next(routes);
            });
    }
}
