import { RouteInstanceProperty } from './routeInstanceProperty.model';

export interface RouteInstance {
  routeInstanceMaps: RouteInstanceMaps[];
  routeInstanceId: number;
  date: string;
  startTime?: string;
  endTime?: string;
  scheduledStartTime?: string;
  scheduledEndTime?: string;
  departureDelayMinutes?: number;
  arrivalDelayMinutes?: number;
  durationHours?: number;
  averageSpeedKmh?: number;
  routeInstanceProperties: RouteInstanceProperty[];
  routeId: number;
  traewellingStatusId?: number;
}

export interface RouteInstanceMaps {
  mapId: number;
}

