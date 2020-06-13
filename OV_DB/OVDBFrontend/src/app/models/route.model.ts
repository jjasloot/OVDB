import { Moment } from 'moment';
import { RouteType } from './routeType.model';
import { RouteCountry } from './routeCountry.model';
import { Map } from './map.model';
import { RoutesMaps } from './routesMaps.model'

export interface Route {
  share: any;
  overrideColour: string;
  routeId: number;
  name: string;
  description?: string;
  lineNumber?: string;
  operatingCompany?: string;
  firstDateTime: Moment;
  routeCountries: RouteCountry[];
  routeMaps: RoutesMaps[];
  routeTypeId: number;
  routeType?: RouteType;
  coordinates: string;
  mapId: number;
  map: Map;
  overrideDistance: number;
  calculatedDistance: number;
}
