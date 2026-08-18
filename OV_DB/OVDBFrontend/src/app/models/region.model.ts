export interface Region {
  id: number;
  name: string;
  nameNL: string;
  originalName: string;
  osmRelationId: number;
  isoCode?: string;
  /** Levels below this country that the region achievement collects; only set on countries. */
  achievementRegionDepth?: number;
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
  /** Of the visited ones, how many you actually got on or off at. A subset, not a separate total. */
  entryExitStations: number;
  children: RegionStat[];
  flagEmoji: string | null;
  parentRegionId: number | null;
}
