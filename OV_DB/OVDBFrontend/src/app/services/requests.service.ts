import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { environment } from "src/environments/environment";
import {
  CreateRequest,
  RequestForAdmin,
  RequestForUser,
} from "../models/requests.model";
import { BehaviorSubject, Observable } from "rxjs";
import { tap } from "rxjs/operators";

@Injectable({
  providedIn: "root",
})
export class RequestsService {
  private httpClient = inject(HttpClient);

  // The toolbar markers subscribe to these. Fetching a request list marks it
  // read server-side, so the flags are cleared there rather than re-queried.
  private hasUnreadSubject = new BehaviorSubject<boolean>(false);
  private hasUnreadAdminSubject = new BehaviorSubject<boolean>(false);
  hasUnread$ = this.hasUnreadSubject.asObservable();
  hasUnreadAdmin$ = this.hasUnreadAdminSubject.asObservable();

  getUserRequests(): Observable<RequestForUser[]> {
    return this.httpClient
      .get<any[]>(environment.backend + "api/requests")
      .pipe(tap(() => this.hasUnreadSubject.next(false)));
  }
  getAdminRequests(): Observable<RequestForAdmin[]> {
    return this.httpClient
      .get<any[]>(environment.backend + "api/requests/admin")
      .pipe(tap(() => this.hasUnreadAdminSubject.next(false)));
  }
  addNewRequest(request: CreateRequest) {
    return this.httpClient.post(environment.backend + "api/requests", request);
  }

  respondToRequest(requestId: number, response: CreateRequest) {
    return this.httpClient.patch(
      environment.backend + "api/requests/admin/" + requestId + "/respond",
      response
    );
  }

  refreshUnreadRequests(): void {
    this.httpClient
      .get<boolean>(environment.backend + "api/requests/anyUnread")
      .subscribe((hasUnread) => this.hasUnreadSubject.next(hasUnread));
  }

  refreshUnreadAdminRequests(): void {
    this.httpClient
      .get<boolean>(environment.backend + "api/requests/admin/anyUnread")
      .subscribe((hasUnread) => this.hasUnreadAdminSubject.next(hasUnread));
  }
}
