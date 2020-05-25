export interface MultipleEdit {
    routeIds: number[];
    updateDate: boolean;
    updateType: boolean;
    updateCountries: boolean;
    updateMaps: boolean;
    date: Date;
    typeId: number;
    countries: number[];
    maps: number[];
}