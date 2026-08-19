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
  /** The kind of trip, shown because the candidate list is otherwise a wall of dates. */
  routeTypeName: string;
  routeTypeNameNL: string;
  routeTypeColour: string;
  /** False only when no train reaches this station, and other trips are offered in their place. */
  isTrain: boolean;
  instances: TripCandidate[];
}

export interface TripCandidate {
  routeInstanceId: number;
  date: string;
  /** Departure and arrival, where known — what tells two trips on one date apart. */
  startTime: string | null;
  endTime: string | null;
}

/** One station's worth of the dating queue. */
export interface StationBackfillItem {
  remaining: number;
  stationId: number;
  stationName: string;
  lattitude: number;
  longitude: number;
  regions: string[];
  candidates: TripCandidateGroup[];
  /**
   * The trip to preselect: the oldest endpoint-grade candidate where one exists, else the oldest
   * of any grade. The oldest candidate overall is endpoint-grade only 15% of the time, so plain
   * "oldest" would usually preselect a train that passed through without stopping.
   */
  suggestedRouteInstanceId: number | null;
  suggestionIsEndpoint: boolean;
  /** The pre-selected route line, sent with the station so drawing it needs no second request. */
  suggestedRouteGeometry: RouteGeometry | null;
}

export interface RouteGeometry {
  routeId: number;
  /** [lattitude, longitude] pairs, the order Leaflet wants. */
  coordinates: [number, number][];
}

/**
 * A station an import says the train called at, that is not marked visited. Comes from an
 * operator's calling pattern — Träwelling stopovers or OSM stop members — never from geometry,
 * which cannot tell stopping from passing through.
 */
export interface StationSuggestion {
  stationId: number;
  stationName: string;
  lattitude: number;
  longitude: number;
  /** Where the journey began or ended, which is where an entry/exit default is honest. */
  isEndpoint: boolean;
  /** How far the upstream stop sat from the OVDB station it was matched to. */
  distanceMetres: number;
}

/** A stop parsed out of an OSM relation's members, as returned by the importer. */
export interface OSMStop {
  name: string;
  lattitude: number;
  longitude: number;
}

/**
 * Suggestions for a just-imported route, with the trip that can date them. A null trip means the
 * route has none yet, so anything marked stays undated and joins the backfill queue.
 */
export interface StationSuggestionsForRoute {
  routeInstanceId: number | null;
  stations: StationSuggestion[];
}
