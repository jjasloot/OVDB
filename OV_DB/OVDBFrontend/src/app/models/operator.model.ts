import { RegionMinimal } from "./region.model";

export interface Operator {
    id: number;
    names: string[];
    regions: RegionMinimal[];
    logoFilePath: string | null;
}

export interface OperatorMinimal {
    id: number;
    name: string;
}

export interface OperatorUpdate {
    names: string[];
    regionIds: number[];
}
