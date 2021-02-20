export interface StationAdminProperties {
  id: number;
  name: string;
  network: string;
  operatingCompany: string;
  lattitude: number;
  longitude: number;
  elevation: number | null;
  hidden: boolean;
  special: boolean;
}
