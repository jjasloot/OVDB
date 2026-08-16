export interface ReplayRoute {
  routeId: number;
  name: string;
  nameNL: string;
  colour: string;
  firstDate: string;
  distanceKm: number;
  /** [latitude, longitude] pairs. */
  coordinates: number[][];
}

export interface Replay {
  start: string | null;
  end: string | null;
  routes: ReplayRoute[];
}
