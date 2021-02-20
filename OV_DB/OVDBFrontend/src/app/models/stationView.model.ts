export interface StationView {
  nameNL: any;
  name: any;
  stations: StationDTO[];
  total: number;
  visited: number;
}

export interface StationDTO {
  id: number;
  name: string;
  lattitude: number;
  longitude: number;
  elevation: number | null;
  network: string;
  operator: string;
  visited: boolean;
}
