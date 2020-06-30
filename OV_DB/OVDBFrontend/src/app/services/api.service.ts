import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Country } from '../models/country.model';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { Route } from '../models/route.model';
import { RouteType } from '../models/routeType.model';
import { Map } from 'src/app/models/map.model';
import { OSMDataLine } from '../models/osmDataLine.model';
import { OSMLineStop } from '../models/osmLineStop.model';
import { Moment } from 'moment';
import { AdminMap } from '../models/adminMap.model';
import { AdminUser } from '../models/adminUser.model';
import { MultipleEdit } from '../models/multipleEdit.model';
import { RouteInstance } from '../models/routeInstance.model';

@Injectable({
  providedIn: 'root'
})
export class ApiService {





  getRoutesWithMissingSettings(): Observable<Route[]> {
    return this.httpClient.get<Route[]>(environment.backend + 'api/routes/missingInfo');
  }

  constructor(private httpClient: HttpClient) { }


  getCountries(guid?: string): Observable<Country[]> {
    if (!guid) {
      return this.httpClient.get<Country[]>(environment.backend + 'api/countries');
    }
    return this.httpClient.get<Country[]>(environment.backend + 'api/mapfilter/countries/' + guid);
  }

  addCountry(country: Country): Observable<any> {
    return this.httpClient.post<Country>(environment.backend + 'api/countries', country);
  }
  updateCountry(country: Country) {
    return this.httpClient.put<Country>(environment.backend + 'api/countries', country);
  }
  deleteCountry(countryId: number): Observable<any> {
    return this.httpClient.delete(environment.backend + 'api/countries/' + countryId);
  }
  getTypes(guid?: string): Observable<RouteType[]> {
    if (!guid) {
      return this.httpClient.get<RouteType[]>(environment.backend + 'api/routetypes');
    }
    return this.httpClient.get<RouteType[]>(environment.backend + 'api/mapfilter/types/' + guid);
  }

  addRouteType(routeType: RouteType): Observable<any> {
    return this.httpClient.post<RouteType>(environment.backend + 'api/routeTypes', routeType);
  }
  updateRouteType(routeType: RouteType) {
    return this.httpClient.put<RouteType>(environment.backend + 'api/routeTypes', routeType);
  }

  deleteRouteType(typeId: number) {
    return this.httpClient.delete(environment.backend + 'api/routetypes/' + typeId);
  }

  getMaps(): Observable<Map[]> {
    return this.httpClient.get<Map[]>(environment.backend + 'api/maps/');
  }

  addMap(map: Map): Observable<any> {
    return this.httpClient.post<RouteType>(environment.backend + 'api/maps', map);
  }
  updateMap(map: Map) {
    return this.httpClient.put<RouteType>(environment.backend + 'api/maps', map);
  }

  deleteMap(mapId: number) {
    return this.httpClient.delete(environment.backend + 'api/maps/' + mapId);
  }
  getYears(guid: string): Observable<number[]> {
    return this.httpClient.get<number[]>(environment.backend + 'api/mapfilter/years/' + guid);
  }

  getRoute(routeId: number): Observable<Route> {
    return this.httpClient.get<Route>(environment.backend + 'api/routes/' + routeId);
  }
  deleteRoute(routeId: number) {
    return this.httpClient.delete(environment.backend + 'api/routes/' + routeId);
  }
  getRouteInstances(routeId: number) {
    return this.httpClient.get<RouteInstance[]>(environment.backend + 'api/routes/instances/' + routeId);
  }
  updateRouteInstance(instance: RouteInstance) {
    return this.httpClient.put(environment.backend + 'api/routes/instances',instance);
  }
  getAllRoutes(start?: number, count?: number, column?: string, order?: boolean, filter?: string): Observable<Route[]> {
    let params = new HttpParams();
    if (start !== undefined && count !== undefined) {
      params = params
        .set('start', start.toString())
        .set('count', count.toString())
    }
    if (column !== undefined) {
      params = params.set('sortColumn', column.toString())
      if (order !== undefined)
        params = params.set('descending', order.toString());
    }
    if (filter !== undefined) {
      params = params.set('filter', filter)
    }
    return this.httpClient.get<Route[]>(environment.backend + 'api/routes', {
      params

    });
  }
  getRouteCount(filter: string): Observable<number> {
    if (!!filter) {
      return this.httpClient.get<number>(environment.backend + 'api/routes/count?filter=' + filter);
    }
    return this.httpClient.get<number>(environment.backend + 'api/routes/count');
  }

  getRoutes(filter: string, guid: string, language: string): Observable<string> {
    let url = "";

    url = environment.backend + 'odata/' + guid;
    if (!!filter) {
      url += '?$filter=' + filter + '&language=' + language;
    }
    return this.httpClient.get<string>(url);
  }

  getSingleRoute(routeId: number, guid: string, language: string): Observable<string> {
    const url = environment.backend + `odata/single/${routeId}/${guid}`;
    return this.httpClient.get<string>(url);
  }


  updateRoute(route: Route): Observable<any> {
    return this.httpClient.put(environment.backend + 'api/routes/' + route.routeId, route);

  }

  postRoute(inputString: string) {
    return this.httpClient.post(environment.backend + 'api/routes/kml', inputString);
  }

  postFiles(fileToUpload: FileList): Observable<any> {
    const formData: FormData = new FormData();
    for (let i = 0; i < fileToUpload.length; i++) {
      formData.append(i.toString(), fileToUpload.item(i), fileToUpload.item(i).name);
    }
    return this.httpClient
      .post(environment.backend + 'api/routes/kmlfile', formData);
  }


  getGuidFromMapName(name: string): Observable<string> {
    return this.httpClient.get<string>(environment.backend + 'api/user/link/' + name);
  }
  getExport(routeId: number) {
    return this.httpClient.get(environment.backend + 'api/routes/' + routeId + '/export', { responseType: 'blob' });
  }
  getCompleteExport() {
    return this.httpClient.get(environment.backend + 'api/routes/export', { responseType: 'blob' });
  }

  updateRouteTypeOrder(newOrder: number[]): Observable<any> {
    return this.httpClient.post<RouteType>(environment.backend + 'api/routeTypes/order', newOrder);
  }
  updateMapOrder(newOrder: number[]) {
    return this.httpClient.post<Map>(environment.backend + 'api/maps/order', newOrder);
  }

  importerGetLines(reference: string, network: string, type: string, dateTime: Moment): Observable<OSMDataLine[]> {
    let url = environment.backend + 'api/importer/find?reference=' + reference;
    if (!!network) {
      url += '&network=' + network;
    }
    if (!!type) {
      url += '&routeType=' + type;
    }
    if (!!dateTime) {
      url += '&dateTime=' + dateTime.toISOString()
    }
    return this.httpClient.get<OSMDataLine[]>(url);

  }
  importerGetNetwork(network: string, dateTime: Moment): Observable<OSMDataLine[]> {
    let url = environment.backend + 'api/importer/network?network=' + network;
    if (!!dateTime) {
      url += '&dateTime=' + dateTime.toISOString()
    }
    return this.httpClient.get<OSMDataLine[]>(url);
  }

  importerGetLine(id: any, from: number = null, to: number = null, dateTime: Moment = null) {
    let url = environment.backend + 'api/importer/' + id;
    if (!!from && !!to) {
      url += '?from=' + from + '&to=' + to;
    }
    if (!!dateTime) {
      if (!!from && !!to) {
        url += '&dateTime=' + dateTime.toISOString();
      } else {
        url += '?dateTime=' + dateTime.toISOString();
      }
    }
    return this.httpClient.get<OSMDataLine>(url);
  }
  importerGetStops(id: any, dateTime: Moment = null): Observable<OSMLineStop[]> {
    let url = environment.backend + 'api/importer/' + id + '/stops';
    if (!!dateTime) {
      url += '?dateTime=' + dateTime.toISOString();
    }
    return this.httpClient.get<OSMLineStop[]>(url);
  }

  importerAddRoute(data: OSMDataLine) {
    const url = environment.backend + 'api/importer/addRoute';

    return this.httpClient.post<Route>(url, data);
  }

  administratorGetMaps(): Observable<AdminMap[]> {
    const url = environment.backend + 'api/admin/maps';

    return this.httpClient.get<AdminMap[]>(url);
  }

  administratorGetUsers(): Observable<AdminUser[]> {
    const url = environment.backend + 'api/admin/users';

    return this.httpClient.get<AdminUser[]>(url);
  }

  updateMultiple(model: MultipleEdit) {
    const url = environment.backend + 'api/routes/editmultiple';

    return this.httpClient.put<MultipleEdit>(url, model);
  }

}
