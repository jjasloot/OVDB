export interface Region {
  id: number;
  name: string;
  nameNL: string;
  originalName:string;
  osmRelationId: number;
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
  originalName:string;
}
