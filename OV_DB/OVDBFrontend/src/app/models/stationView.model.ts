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
  /** Null while visited is true means the level is not known yet — a legacy row awaiting backfill. */
  visitLevel: StationVisitLevel | null;
}

/** The state of a single visit after marking, un-marking or changing its level. */
export interface StationVisitState {
  visited: boolean;
  level: StationVisitLevel | null;
  firstStoppedDate: string | null;
  firstEntryExitDate: string | null;
  percentageVisited: number;
}
