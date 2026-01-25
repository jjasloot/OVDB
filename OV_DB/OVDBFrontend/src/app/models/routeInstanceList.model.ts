import { RouteInstance } from "./routeInstance.model";

export interface RouteInstanceListDTO extends RouteInstance {
  routeName: string;
  routeDescription: string;
  routeType: string;
  routeTypeColour: string;
  from: string;
  to: string;
  distance: number;
  routeOverrideColour: string;
}

export interface RouteInstanceListResponseDTO {
  count: number;
  instances: RouteInstanceListDTO[];
}
