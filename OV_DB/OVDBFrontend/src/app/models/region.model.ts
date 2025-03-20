export interface Region {
  id: number;
  name: string;
  nameNL: string;
  originalName: string;
  osmRelationId: number;
  subRegions: Region[];
  intermediateRegions: Region[]; // Added for intermediate regions
}

export interface NewRegion {
  osmRelationId: number;
  parentRegionId: number | null;
  intermediateRegionId: number | null; // Added for intermediate region
}

export interface RegionMinimal {
  id: number;
  name: string;
  nameNL: string;
  originalName: string;
}
