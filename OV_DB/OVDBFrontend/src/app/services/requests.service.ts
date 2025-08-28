import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { environment } from "src/environments/environment";
import {
  CreateRequest,
  RequestForAdmin,
  RequestForUser,
} from "../models/requests.model";
import { Observable } from "rxjs";

@Injectable({
  providedIn: "root",
})
export class RequestsService {
  private httpClient = inject(HttpClient);

  getUserRequests(): Observable<RequestForUser[]> {
    return this.httpClient.get<any[]>(environment.backend + "api/requests");
  }
  getAdminRequests(): Observable<RequestForAdmin[]> {
    return this.httpClient.get<any[]>(
      environment.backend + "api/requests/admin"
    );
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

  hasAnyUnreadRequests(): Observable<boolean> {
    return this.httpClient.get<boolean>(
      environment.backend + "api/requests/anyUnread"
    );
  }

  adminHasAnyUnreadRequests(): Observable<boolean> {
    return this.httpClient.get<boolean>(
      environment.backend + "api/requests/admin/anyUnread"
    );
  }
}
