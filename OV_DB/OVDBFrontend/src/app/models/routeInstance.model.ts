import { RouteInstanceProperty } from './routeInstanceProperty.model';

export interface RouteInstance {
  routeInstanceMaps: RouteInstanceMaps[];
  routeInstanceId: number;
  date: Date;
  startTime?: Date;
  endTime?: Date;
  durationHours?: number;
  routeInstanceProperties: RouteInstanceProperty[];
  routeId: number;
}

export interface RouteInstanceMaps {
  mapId: number;
}
