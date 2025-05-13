export interface RegionOperators {
    regionId: number;
    name: string;
    nameNL: string;
    originalName: string;
    operators: RegionOperator[];
}

export interface RegionOperator {
    operatorId: number;
    operatorNames: string[];
    hasUserRoute: boolean;
}