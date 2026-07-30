import { Region, RegionMinimal } from "./region.model";

export interface StationMap {
  id: number;
  name: string;
  nameNL: string;
  mapGuid: string;
  sharingLinkName: string | null;
  includeSpecials: boolean;
  regionIds: number[]
}

export interface StationMapCountryDTO {
  stationCountryId: number;
  includeSpecials: boolean;
}
