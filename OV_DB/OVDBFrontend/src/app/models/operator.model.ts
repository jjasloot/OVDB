import { RegionMinimal } from "./region.model";

export interface Operator {
  id: number;
  names: string[];
  runsTrainsInRegions: RegionMinimal[];
  restrictToRegions: RegionMinimal[];
  logoFilePath: string | null;
}

export interface OperatorMinimal {
  id: number;
  name: string;
}

export interface OperatorUpdate {
  names: string[];
  runsTrainsInRegionIds: number[];
  restrictToRegionsIds: number[];
}
