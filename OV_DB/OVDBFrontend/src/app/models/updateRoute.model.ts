import { DateTime } from "luxon";

export interface UpdateRoute {
  overrideColour: string;
  routeId: number;
  name: string;
  description?: string;
  to?: string;
  from?: string;
  lineNumber?: string;
  operatingCompany?: string;
  firstDateTime: DateTime;
  countries: number[];
  maps: number[];
  routeTypeId: number;
  operatorIds: number[];
}
