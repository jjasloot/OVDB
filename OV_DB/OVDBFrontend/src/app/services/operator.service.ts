import { Injectable, Signal, inject } from "@angular/core";
import { environment } from "src/environments/environment";
import {
  Operator,
  OperatorMinimal,
  OperatorUpdate,
} from "../models/operator.model";
import { HttpClient } from "@angular/common/http";
import { Observable, of } from "rxjs";
import { map, shareReplay, tap } from "rxjs/operators";

@Injectable({
  providedIn: "root",
})
export class OperatorService {
  private httpClient = inject(HttpClient);


  logos = new Map<number, Observable<string>>();

  getOperators() {
    return this.httpClient.get<Operator[]>(
      environment.backend + "api/operators"
    );
  }

  getOperator(id: number) {
    return this.httpClient.get<Operator>(
      environment.backend + "api/operators/" + id
    );
  }

  addOperator(operator: Operator) {
    return this.httpClient.post<OperatorUpdate>(
      environment.backend + "api/operators",
      operator
    );
  }

  updateOperator(id: number, operator: OperatorUpdate) {
    return this.httpClient.put(
      environment.backend + "api/operators/" + id,
      operator
    );
  }

  deleteOperator(id: number) {
    return this.httpClient.delete(environment.backend + "api/operators/" + id);
  }

  uploadOperatorLogo(id: number, file: File) {
    const formData = new FormData();
    formData.append("file", file);
    return this.httpClient
      .post(
        environment.backend + "api/operators/" + id + "/uploadLogo",
        formData
      )
      .pipe(tap(() => this.logos.delete(id)));
  }

  getOperatorNames() {
    return this.httpClient.get<OperatorMinimal[]>(
      environment.backend + "api/operators/minimal"
    );
  }

  getOperatorNamesForRoute(routeId: number) {
    return this.httpClient.get<OperatorMinimal[]>(
      environment.backend + "api/Operators/forRoute/" + routeId
    );
  }

  getOperatorLogo(id: number): Observable<string> {
    if (this.logos.has(id)) {
      return this.logos.get(id);
    }
    this.logos.set(
      id,
      this.httpClient
        .get(environment.backend + "api/operators/" + id + "/logo", {
          responseType: "blob",
        })
        .pipe(
          map((blob) => URL.createObjectURL(blob as Blob)),
          shareReplay()
        )
    );
    return this.logos.get(id);
  }

  connectRoutesToOperator(id: number) {
    return this.httpClient.patch(
      environment.backend + "api/operators/" + id + "/connect",
      {}
    );
  }

  getOpenOperatorsForRegion(regionId: number) {
    return this.httpClient.get<string[]>(
      environment.backend + "api/operators/openOperators/" + regionId
    );
  }
}
