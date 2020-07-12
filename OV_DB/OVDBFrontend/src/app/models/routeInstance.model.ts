import { RouteInstanceProperty } from './routeInstanceProperty.model';

export interface RouteInstance {
  routeInstanceId: number;
  date: Date;
  routeInstanceProperties: RouteInstanceProperty[];
  routeId: number;
}
