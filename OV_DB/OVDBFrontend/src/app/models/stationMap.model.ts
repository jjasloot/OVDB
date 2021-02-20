export interface StationMap {
  stationMapId: number;
  name: string;
  nameNL: string;
  mapGuid: string;
  sharingLinkName: string | null;
  stationMapCountries: StationMapCountryDTO[]
}

export interface StationMapCountryDTO {
  stationCountryId: number;
  includeSpecials: boolean;
}
