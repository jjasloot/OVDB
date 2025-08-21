import { RouteInstanceProperty } from './routeInstanceProperty.model';

export interface RouteInstance {
  routeInstanceMaps: RouteInstanceMaps[];
  routeInstanceId: number;
  date: string;
  startTime?: string;
  endTime?: string;
  durationHours?: number;
  averageSpeedKmh?: number;
  routeInstanceProperties: RouteInstanceProperty[];
  routeId: number;
  traewellingStatusId?: number;
}

export interface RouteInstanceMaps {
  mapId: number;
}
