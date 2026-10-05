export interface RouteType {
    typeId?: number;
    name: string;
    nameNL: string;
    colour: string;
    isTrain: boolean;
    trainlogType: string;
    /** Routes of this type run on rails and count towards rail network completion. */
    matchesRailNetworks: boolean;
}
