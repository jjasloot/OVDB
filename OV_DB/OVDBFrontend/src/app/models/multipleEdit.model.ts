export interface MultipleEdit {
    routeIds: number[];
    updateDate: boolean;
    updateType: boolean;
    updateMaps: boolean;
    /** yyyy-MM-dd */
    date?: string;
    typeId: number;
    countries: number[];
    maps: number[];
}
