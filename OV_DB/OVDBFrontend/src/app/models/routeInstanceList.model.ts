import { RouteInstance } from "./routeInstance.model";

export interface RouteInstanceListDTO extends RouteInstance {
  routeName: string;
  routeDescription: string;
  routeType: RouteTypeDTO;
  routeTypeColour: string;
  from: string;
  to: string;
  distance: number;
  routeOverrideColour: string;
  share: string;
}

export interface RouteInstanceListResponseDTO {
  count: number;
  instances: RouteInstanceListDTO[];
}
export interface RouteTypeDTO {
  name: string;
  nameNL: string;
}