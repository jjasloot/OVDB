export interface Region {
  id: number;
  name: string;
  nameNL: string;
  originalName: string;
  osmRelationId: number;
  isoCode?: string;
  subRegions: Region[];
}

export interface NewRegion {
  osmRelationId: number;
  parentRegionId: number | null;
}

export interface RegionMinimal {
  id: number;
  name: string;
  nameNL: string;
  originalName: string;
}

export interface RegionStat {
  id: number;
  name: string;
  nameNL: string;
  originalName: string;
  osmRelationId: number;
  visited: boolean;
  totalStations: number;
  visitedStations: number;
  children: RegionStat[];
  flagEmoji: string | null;
  parentRegionId: number | null;
}
