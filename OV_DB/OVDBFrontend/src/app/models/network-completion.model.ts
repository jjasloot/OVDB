import { GeoJsonObject } from 'geojson';

/** Mirrors OVDB_database.Models.NetworkMode. */
export enum NetworkMode {
  Rail = 0,
  UrbanRail = 1,
}

/** Mirrors OVDB_database.Models.NetworkStatus. */
export enum NetworkStatus {
  Queued = 0,
  Assembling = 1,
  Ready = 2,
  Failed = 3,
}

export interface NetworkStatusRow {
  id: number;
  mode: NetworkMode;
  status: NetworkStatus;
  assembledAt: string | null;
  osmDataTimestamp: string | null;
  edgeCount: number;
  version: number;
  totalKm: number;
  bonusKm: number;
  classifiedByRelations: boolean;
  error: string | null;
}

export interface RegionNetworkSummary {
  id: number;
  mode: NetworkMode;
  version: number;
  totalKm: number;
  bonusKm: number;
  edgeCount: number;
  assembledAt: string | null;
  classifiedByRelations: boolean;
}

export interface RegionNetworks {
  regionId: number;
  name: string;
  nameNL: string;
  parentRegionId: number | null;
  networks: RegionNetworkSummary[];
}

export interface NetworkGeometryEdge {
  id: number;
  name: string | null;
  fromStation: string | null;
  toStation: string | null;
  lengthKm: number;
  isPassenger: boolean;
  geometry: GeoJsonObject;
}

export interface NetworkGeometry {
  version: number;
  totalKm: number;
  bonusKm: number;
  edges: NetworkGeometryEdge[];
}

export interface CoveredEdge {
  id: number;
  /** [start, end] fractions of the simplified geometry's length. */
  intervals: [number, number][];
}

export interface NetworkCompletion {
  percent: number;
  coveredKm: number;
  totalKm: number;
  bonusKm: number;
  edges: CoveredEdge[];
}

/** Mirrors OVDB_database.Models.NetworkOverrideKind. */
export enum NetworkOverrideKind {
  Excluded = 0,
  ForceRail = 1,
  ForceUrbanRail = 2,
}

export interface NetworkWayOverride {
  id: number;
  osmWayId: number;
  kind: NetworkOverrideKind;
  createdAt: string;
}

export interface MissingRun {
  name: string;
  fromStation: string | null;
  toStation: string | null;
  missingKm: number;
  edgeIds: number[];
}
