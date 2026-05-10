export interface StationNearbyPair {
  station1Id: number;
  station1Name: string;
  station1Lattitude: number;
  station1Longitude: number;
  station1Visits: number;
  station1Special: boolean;
  station2Id: number;
  station2Name: string;
  station2Lattitude: number;
  station2Longitude: number;
  station2Visits: number;
  station2Special: boolean;
  distanceMeters: number;
}

export interface StationMergeRegion {
  regionId: number;
  regionName: string;
  pairCount: number;
}

export interface StationMergePairsResponse {
  total: number;
  pairs: StationNearbyPair[];
}
