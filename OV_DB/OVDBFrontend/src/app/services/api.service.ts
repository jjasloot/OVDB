import { Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { Country } from "../models/country.model";
import { HttpClient, HttpParams } from "@angular/common/http";
import { environment } from "src/environments/environment";
import { Route, RouteList } from "../models/route.model";
import { RouteType } from "../models/routeType.model";
import { Map } from "src/app/models/map.model";
import { OSMDataLine } from "../models/osmDataLine.model";
import { OSMLineStop } from "../models/osmLineStop.model";
import { Moment } from "moment";
import { AdminMap } from "../models/adminMap.model";
import { AdminUser } from "../models/adminUser.model";
import { MultipleEdit } from "../models/multipleEdit.model";
import { RouteInstance } from "../models/routeInstance.model";
import { StationMap } from "../models/stationMap.model";
import { StationView } from "../models/stationView.model";
import { StationCountry } from "../models/stationCountry.model";
import { StationAdminProperties } from "../models/stationAdminProperties.model";
import { MapDataDTO } from "../models/map-data.model";
import { RegionOperators } from "../models/region-operators.model";
import { RegionStat } from "../models/region.model";
import { UserProfile, UpdateProfile, ChangePassword } from "../models/user-profile.model";
import { 
  TrawellingConnectionStatus, 
  TrawellingConnectResponse, 
  TrawellingOAuthRequest,
  TrawellingUnimportedResponse,
  TrawellingImportRequest,
  TrawellingImportResponse,
  TrawellingStats,
  TrawellingProcessBacklogRequest,
  TrawellingProcessBacklogResponse,
  RouteInstanceSearchResult,
  LinkToRouteInstanceRequest,
  LinkToRouteInstanceResponse
} from "../models/traewelling.model";

@Injectable({
  providedIn: "root",
})
export class ApiService {

  getRoutesWithMissingSettings(): Observable<Route[]> {
    return this.httpClient.get<Route[]>(
      environment.backend + "api/routes/missingInfo"
    );
  }

  constructor(private httpClient: HttpClient) { }

  getCountries(guid?: string): Observable<Country[]> {
    if (!guid) {
      return this.httpClient.get<Country[]>(
        environment.backend + "api/countries"
      );
    }
    return this.httpClient.get<Country[]>(
      environment.backend + "api/mapfilter/countries/" + guid
    );
  }

  addCountry(country: Country): Observable<any> {
    return this.httpClient.post<Country>(
      environment.backend + "api/countries",
      country
    );
  }
  updateCountry(country: Country) {
    return this.httpClient.put<Country>(
      environment.backend + "api/countries",
      country
    );
  }
  deleteCountry(countryId: number): Observable<any> {
    return this.httpClient.delete(
      environment.backend + "api/countries/" + countryId
    );
  }
  getTypes(guid?: string): Observable<RouteType[]> {
    if (!guid) {
      return this.httpClient.get<RouteType[]>(
        environment.backend + "api/routetypes"
      );
    }
    return this.httpClient.get<RouteType[]>(
      environment.backend + "api/mapfilter/types/" + guid
    );
  }

  addRouteType(routeType: RouteType): Observable<any> {
    return this.httpClient.post<RouteType>(
      environment.backend + "api/routeTypes",
      routeType
    );
  }
  updateRouteType(routeType: RouteType) {
    return this.httpClient.put<RouteType>(
      environment.backend + "api/routeTypes",
      routeType
    );
  }

  deleteRouteType(typeId: number) {
    return this.httpClient.delete(
      environment.backend + "api/routetypes/" + typeId
    );
  }

  getMaps(): Observable<Map[]> {
    return this.httpClient.get<Map[]>(environment.backend + "api/maps/");
  }

  addMap(map: Map): Observable<any> {
    return this.httpClient.post<RouteType>(
      environment.backend + "api/maps",
      map
    );
  }
  updateMap(map: Map) {
    return this.httpClient.put<RouteType>(
      environment.backend + "api/maps",
      map
    );
  }

  deleteMap(mapId: number) {
    return this.httpClient.delete(environment.backend + "api/maps/" + mapId);
  }
  getYears(guid: string): Observable<number[]> {
    return this.httpClient.get<number[]>(
      environment.backend + "api/mapfilter/years/" + guid
    );
  }

  getRoute(routeId: number): Observable<Route> {
    return this.httpClient.get<Route>(
      environment.backend + "api/routes/" + routeId
    );
  }
  assignRegionsToRoute(routeId: number) {
    return this.httpClient.patch(
      environment.backend + "api/routes/" + routeId + "/assignRegions",
      {}
    );
  }
  deleteRoute(routeId: number) {
    return this.httpClient.delete(
      environment.backend + "api/routes/" + routeId
    );
  }
  getRouteInstances(routeId: number, from?, to?) {
    let url = environment.backend + "api/routes/instances/" + routeId;
    if (!!from && !!to) {
      url += `?from=${from}&to=${to}`;
    }
    return this.httpClient.get<Route>(url);
  }
  getRouteInstancesForMap(mapGuid: string, routeId: number, from?, to?) {
    let url =
      environment.backend + "api/routes/instances/" + mapGuid + "/" + routeId;
    if (!!from && !!to) {
      url += `?from=${from}&to=${to}`;
    }
    return this.httpClient.get<Route>(url);
  }
  updateRouteInstance(instance: RouteInstance) {
    return this.httpClient.put(
      environment.backend + "api/routes/instances",
      instance
    );
  }

  deleteRouteInstance(routeInstanceId: number): Observable<any> {
    return this.httpClient.delete(
      environment.backend + "api/routes/instances/" + routeInstanceId,
      {}
    );
  }
  getAllRoutes(
    start?: number,
    count?: number,
    column?: string,
    order?: boolean,
    filter?: string
  ): Observable<RouteList> {
    let params = new HttpParams();
    if (start !== undefined && count !== undefined) {
      params = params
        .set("start", start.toString())
        .set("count", count.toString());
    }
    if (column !== undefined) {
      params = params.set("sortColumn", column.toString());
      if (order !== undefined) {
        params = params.set("descending", order.toString());
      }
    }
    if (filter !== undefined) {
      params = params.set("filter", filter);
    }
    return this.httpClient.get<RouteList>(environment.backend + "api/routes", {
      params,
    });
  }

  getRoutes(
    filter: string,
    guid: string,
    language: string,
    includeLineColours: boolean,
    limitToSelectedAreas: boolean,
    identifier?: string
  ): Observable<MapDataDTO> {
    let url = "";

    url = environment.backend + "odata/" + guid;
    let params = new HttpParams();
    if (filter) {
      params = params.append("$filter", filter);
    }
    if (language) {
      params = params.append("language", language);
    }
    params = params.append("limitToSelectedArea", limitToSelectedAreas);
    params = params.append("includeLineColours", includeLineColours);
    if (identifier) {
      params = params.append("requestIdentifier", identifier);
    }
    return this.httpClient.get<MapDataDTO>(url, { params });
  }

  getSingleRoute(
    routeId: number,
    guid: string,
    language: string
  ): Observable<string> {
    const url = environment.backend + `odata/single/${routeId}/${guid}`;
    return this.httpClient.get<string>(url);
  }

  updateRoute(route: Route): Observable<any> {
    return this.httpClient.put(
      environment.backend + "api/routes/" + route.routeId,
      route
    );
  }

  postRoute(inputString: string) {
    return this.httpClient.post(
      environment.backend + "api/routes/kml",
      inputString
    );
  }

  postFiles(fileToUpload: FileList): Observable<any> {
    const formData: FormData = new FormData();
    for (let i = 0; i < fileToUpload.length; i++) {
      formData.append(
        i.toString(),
        fileToUpload.item(i),
        fileToUpload.item(i).name
      );
    }
    return this.httpClient.post(
      environment.backend + "api/routes/kmlfile",
      formData
    );
  }

  getGuidFromMapName(name: string): Observable<string> {
    return this.httpClient.get<string>(
      environment.backend + "api/user/link/" + name
    );
  }

  getGuidFromStationMapName(name: string): Observable<string> {
    return this.httpClient.get<string>(
      environment.backend + "api/user/station-link/" + name
    );
  }

  getExport(routeId: number) {
    return this.httpClient.get(
      environment.backend + "api/routes/" + routeId + "/export",
      { responseType: "blob" }
    );
  }
  getCompleteExport(map?: string, year?: number) {
    let params = new HttpParams();
    if (map != null) {
      params = params.append("map", map);
    }
    if (year != null) {
      params = params.append("year", year);
    }
    return this.httpClient.get(environment.backend + "api/routes/export", {
      params,
      responseType: "blob",
    });
  }

  getExportForSet(selectedRoutes: number[]) {
    let params = new HttpParams();
    params = params.append("routeIds", selectedRoutes.join(','));
    return this.httpClient.get(environment.backend + "api/routes/exportSet", {
      params,
      responseType: "blob",
    });
  }

  updateRouteTypeOrder(newOrder: number[]): Observable<any> {
    return this.httpClient.post<RouteType>(
      environment.backend + "api/routeTypes/order",
      newOrder
    );
  }
  updateMapOrder(newOrder: number[]) {
    return this.httpClient.post<Map>(
      environment.backend + "api/maps/order",
      newOrder
    );
  }

  importerGetLines(
    reference: string,
    network: string,
    type: string,
    dateTime: Moment
  ): Observable<OSMDataLine[]> {
    let url = environment.backend + "api/importer/find?reference=" + reference;
    if (network) {
      url += "&network=" + network;
    }
    if (type) {
      url += "&routeType=" + type;
    }
    if (dateTime) {
      url += "&dateTime=" + dateTime.toISOString();
    }
    return this.httpClient.get<OSMDataLine[]>(url);
  }
  importerGetNetwork(
    network: string,
    dateTime: Moment
  ): Observable<OSMDataLine[]> {
    let url = environment.backend + "api/importer/network?network=" + network;
    if (dateTime) {
      url += "&dateTime=" + dateTime.toISOString();
    }
    return this.httpClient.get<OSMDataLine[]>(url);
  }

  importerGetLine(
    id: any,
    from: number = null,
    to: number = null,
    dateTime: Moment = null
  ) {
    let url = environment.backend + "api/importer/" + id;
    if (!!from && !!to) {
      url += "?from=" + from + "&to=" + to;
    }
    if (dateTime) {
      if (!!from && !!to) {
        url += "&dateTime=" + dateTime.toISOString();
      } else {
        url += "?dateTime=" + dateTime.toISOString();
      }
    }
    return this.httpClient.get<OSMDataLine>(url);
  }
  importerGetStops(
    id: any,
    dateTime: Moment = null
  ): Observable<OSMLineStop[]> {
    let url = environment.backend + "api/importer/" + id + "/stops";
    if (dateTime) {
      url += "?dateTime=" + dateTime.toISOString();
    }
    return this.httpClient.get<OSMLineStop[]>(url);
  }

  importerAddRoute(data: OSMDataLine) {
    const url = environment.backend + "api/importer/addRoute";

    return this.httpClient.post<Route>(url, data);
  }

  administratorGetMaps(): Observable<AdminMap[]> {
    const url = environment.backend + "api/admin/maps";

    return this.httpClient.get<AdminMap[]>(url);
  }

  administratorGetUsers(): Observable<AdminUser[]> {
    const url = environment.backend + "api/admin/users";

    return this.httpClient.get<AdminUser[]>(url);
  }

  updateMultiple(model: MultipleEdit) {
    const url = environment.backend + "api/routes/editmultiple";

    return this.httpClient.put<MultipleEdit>(url, model);
  }
  getStatsForGraph(map: string, year?: number): Observable<any> {
    let url = environment.backend + "api/stats/time/" + map;
    if (year) {
      url += `?year=${year}`;
    }
    return this.httpClient.get(url);
  }

  getStats(map: string, year: number) {
    let url = environment.backend + "api/stats/" + map;
    if (year) {
      url += `?year=${year}`;
    }
    return this.httpClient.get(url);
  }

  getStatsReach(map: string, year: number) {
    let url = environment.backend + "api/stats/reach/" + map;
    if (year) {
      url += `?year=${year}`;
    }
    return this.httpClient.get(url);
  }

  getAutocompleteForTags() {
    const url = environment.backend + "api/routes/instances/tags/autocomplete";

    return this.httpClient.get<string[]>(url);
  }

  getStationMap(guid) {
    const url = environment.backend + "api/stationmaps/map/" + guid;
    return this.httpClient.get<StationView>(url);
  }
  getStationsAdminMap(
    regions: number[] = []
  ): Observable<StationAdminProperties[]> {
    let url = environment.backend + "api/station/map/";
    if (regions.length > 0) {
      url += "?regions=" + regions.join(",");
    }
    return this.httpClient.get<StationAdminProperties[]>(url);
  }

  updateStation(id: any, value: any) {
    const url = environment.backend + "api/station/" + id;
    return this.httpClient.put(url, { value });
  }

  updateStationAdmin(id: any, special: any, hidden) {
    const url = environment.backend + "api/station/admin/" + id;
    return this.httpClient.put(url, { special, hidden });
  }
  deleteStationAdmin(id: number) {
    const url = environment.backend + "api/station/admin/" + id;
    return this.httpClient.delete(url);
  }
  updateStationsInRegion(regionId: number) {
    const url = environment.backend + "api/StationImporter/region/" + regionId;
    return this.httpClient.post(url, {});
  }

  importStation(osmId: string) {
    const url = environment.backend + "api/StationImporter/" + osmId;
    return this.httpClient.post(url, {});
  }

  listStationMaps() {
    const url = environment.backend + "api/stationmaps";
    return this.httpClient.get<StationMap[]>(url);
  }

  updateStationMapOrder(newOrder: number[]) {
    return this.httpClient.post<Map>(
      environment.backend + "api/stationmaps/order",
      newOrder
    );
  }

  addStationMap(stationMap: StationMap): Observable<any> {
    return this.httpClient.post<RouteType>(
      environment.backend + "api/stationmaps",
      stationMap
    );
  }
  updateStationMap(stationMap: StationMap) {
    return this.httpClient.put<RouteType>(
      environment.backend + "api/stationmaps",
      stationMap
    );
  }

  deleteStationMap(stationMapId: number) {
    return this.httpClient.delete(
      environment.backend + "api/stationmaps/" + stationMapId
    );
  }
  getStationCountries(): Observable<StationCountry[]> {
    return this.httpClient.get<StationCountry[]>(
      environment.backend + "api/stationmaps/countries"
    );
  }
  getTripReport(selectedMap: string, selectedYear: number) {
    const url = `${environment.backend}api/tripreport?guid=${selectedMap}&year=${selectedYear}`;
    return this.httpClient.get(url, { responseType: "blob" });
  }

  getOperatorsGroupedByRegion(): Observable<RegionOperators[]> {
    return this.httpClient.get<RegionOperators[]>(
      environment.backend + "api/operators/groupedByRegion"
    );
  }

  getRegionStats(): Observable<RegionStat[]> {
    return this.httpClient.get<RegionStat[]>(
      environment.backend + "api/stats/region"
    )
  }

  // User Profile methods
  getUserProfile(): Observable<UserProfile> {
    return this.httpClient.get<UserProfile>(
      environment.backend + "api/user/profile"
    );
  }

  updateUserProfile(profile: UpdateProfile): Observable<any> {
    return this.httpClient.put(
      environment.backend + "api/user/profile",
      profile
    );
  }

  changePassword(changePassword: ChangePassword): Observable<any> {
    return this.httpClient.post(
      environment.backend + "api/user/change-password",
      changePassword
    );
  }

  // Träwelling API methods
  getTrawellingConnectUrl(): Observable<TrawellingConnectResponse> {
    return this.httpClient.get<TrawellingConnectResponse>(
      environment.backend + "api/traewelling/connect"
    );
  }

  handleTrawellingCallback(request: TrawellingOAuthRequest): Observable<any> {
    return this.httpClient.post(
      environment.backend + "api/traewelling/callback",
      request
    );
  }

  getTrawellingStatus(): Observable<TrawellingConnectionStatus> {
    return this.httpClient.get<TrawellingConnectionStatus>(
      environment.backend + "api/traewelling/status"
    );
  }

  getTrawellingUnimported(page: number = 1): Observable<TrawellingUnimportedResponse> {
    const params = new HttpParams().set('page', page.toString());
    return this.httpClient.get<TrawellingUnimportedResponse>(
      environment.backend + "api/traewelling/unimported",
      { params }
    );
  }

  importTrawellingTrip(request: TrawellingImportRequest): Observable<TrawellingImportResponse> {
    return this.httpClient.post<TrawellingImportResponse>(
      environment.backend + "api/traewelling/import",
      request
    );
  }

  processTrawellingBacklog(request?: TrawellingProcessBacklogRequest): Observable<TrawellingProcessBacklogResponse> {
    return this.httpClient.post<TrawellingProcessBacklogResponse>(
      environment.backend + "api/traewelling/process-backlog",
      request || {}
    );
  }

  disconnectTraewelling(): Observable<any> {
    return this.httpClient.delete(
      environment.backend + "api/traewelling/disconnect"
    );
  }

  getTrawellingStats(): Observable<TrawellingStats> {
    return this.httpClient.get<TrawellingStats>(
      environment.backend + "api/traewelling/stats"
    );
  }

  searchRouteInstances(date: string, query?: string): Observable<RouteInstanceSearchResult[]> {
    let params = new HttpParams().set('date', date);
    if (query) {
      params = params.set('query', query);
    }
    return this.httpClient.get<RouteInstanceSearchResult[]>(
      environment.backend + "api/traewelling/route-instances",
      { params }
    );
  }

  linkToRouteInstance(request: LinkToRouteInstanceRequest): Observable<LinkToRouteInstanceResponse> {
    return this.httpClient.post<LinkToRouteInstanceResponse>(
      environment.backend + "api/traewelling/link",
      request
    );
  }
}
