import { RouteInstanceProperty } from './routeInstanceProperty.model';

export interface RouteInstance {
  routeInstanceMaps: RouteInstanceMaps[];
  routeInstanceId: number;
  date: Date;
  routeInstanceProperties: RouteInstanceProperty[];
  routeId: number;
}

export interface RouteInstanceMaps {
  mapId: number;
}
