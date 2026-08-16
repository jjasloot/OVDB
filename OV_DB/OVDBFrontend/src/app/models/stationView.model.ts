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
  firstEntryExitDate: string | null;
  percentageVisited: number;
}
