export interface MapDataDTO {
  routes: any;

  area: MapBoundsDTO | null;
}

export interface MapBoundsDTO {
  southEast: Position;
  northWest: Position;
}

export interface Position {
  latitude: number;
  longitude: number;
}
