export interface ReplayRoute {
  routeId: number;
  name: string;
  nameNL: string;
  /** The colour actually used: the route's override where it has one. */
  colour: string;
  /** The type's own colour, so overrides can be turned off and compared. */
  routeTypeColour: string;
  firstDate: string;
  distanceKm: number;
  /** [latitude, longitude] pairs. */
  coordinates: number[][];
}

export interface Replay {
  start: string | null;
  end: string | null;
  routes: ReplayRoute[];
  /** When each dated visit first became a stop, sorted. Undated visits are absent. */
  stoppedDates: string[];
  /**
   * When each dated visit first became a got-on/off, sorted. A subset of stoppedDates by date or
   * later, so the gap between the two counts is the stations still only stopped at.
   */
  entryExitDates: string[];
  /** Per-country breakdown of the same growth. */
  regions: ReplayRegion[];
}

/** One country's station progress over the replay's timeline. */
export interface ReplayRegion {
  regionId: number;
  name: string;
  nameNL: string;
  flagEmoji: string | null;
  /** Active stations in the country — what the bar fills towards. */
  totalStations: number;
  stoppedDates: string[];
  entryExitDates: string[];
}
