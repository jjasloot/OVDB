import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { environment } from "src/environments/environment";
import { NewRegion, Region } from "../models/region.model";

@Injectable({
  providedIn: "root",
})
export class RegionsService {
  constructor(private httpClient: HttpClient) {}

  getRegions() {
    return this.httpClient.get<Region[]>(environment.backend + "api/regions");
  }

  addRegion(region: NewRegion) {
    return this.httpClient.post<Region>(
      environment.backend + "api/regions",
      region
    );
  }

  getMapsRegions(mapGuid: string) {
    return this.httpClient.get<Region[]>(
      environment.backend + "api/regions/map/" + mapGuid
    );
  }

  refreshRoutesForRegion(regionId: number) {
    return this.httpClient.post(
      environment.backend + "api/regions/" + regionId + "/refreshRoutes",
      {}
    );
  }
  refreshRoutesWithoutRegions(){
    return this.httpClient.post(
      environment.backend + "api/regions/refreshRoutesWithoutRegions",
      {}
    );
  }
}
