/** What a visit means. Mirrors the API enum; the level is derived server-side from the dates. */
export enum StationVisitLevel {
  Stopped = 0,
  EntryExit = 1,
}

export interface StationView {
  nameNL: any;
  name: any;
  stations: StationDTO[];
  total: number;
  visited: number;
}

export interface StationDTO {
  id: number;
  name: string;
  lattitude: number;
  longitude: number;
  elevation: number | null;
  network: string;
  operator: string;
  visited: boolean;
  /** Null only when unvisited: every visit is at least a stop, dated or not. */
  visitLevel: StationVisitLevel | null;
  /** Null is the ordinary case for now — the web marks without claiming a date. */
  firstStoppedDate: string | null;
  firstEntryExitDate: string | null;
}

/** The state of a single visit after marking, un-marking or changing its level. */
export interface StationVisitState {
  visited: boolean;
  level: StationVisitLevel | null;
  firstStoppedDate: string | null;
  firstStoppedRouteInstanceId: number | null;
  firstEntryExitDate: string | null;
  firstEntryExitRouteInstanceId: number | null;
  percentageVisited: number;
}

/**
 * Both dates as the user says they are, rather than as a floor to improve on. A trip id wins over
 * the date beside it: the server takes the date from the trip so the pair cannot drift apart.
 */
export interface StationVisitDates {
  firstStoppedDate: string | null;
  firstStoppedRouteInstanceId: number | null;
  firstEntryExitDate: string | null;
  firstEntryExitRouteInstanceId: number | null;
}

/**
 * Candidate trips for one station, one entry per route. A station averages ~61 candidate trips
 * across ~26 routes, so the raw list is unreadable; per route only the earliest instance can answer
 * "when did I first come here", and the rest sit behind the row.
 */
export interface TripCandidateGroup {
  routeId: number;
  routeName: string;
  from: string;
  to: string;
  /** The route starts or ends here — the only evidence you stood on the platform. */
  isEndpoint: boolean;
  distanceMetres: number;
  instances: TripCandidate[];
}

export interface TripCandidate {
  routeInstanceId: number;
  date: string;
}
