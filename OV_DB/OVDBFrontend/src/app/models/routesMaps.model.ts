import { Map } from './map.model';
import { Route } from './route.model';

export interface RoutesMaps {
    routeId: number;
    mapId: number;
    map: Map;
    route: Route;
}