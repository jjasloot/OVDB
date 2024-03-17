import { Moment } from "moment";

export class FilterSettings {
  constructor(
    name: string,
    includeLineColours: boolean,
    from?: Moment,
    to?: Moment,
    selectedCountries?: number[],
    selectedTypes?: number[],
    selectedYears?: number[]
  ) {
    this.name = name;
    this.from = from || null;
    this.to = to || null;
    this.selectedCountries = selectedCountries || [];
    this.selectedTypes = selectedTypes || [];
    this.selectedYears = selectedYears || [];
    this.includeLineColours = includeLineColours;
  }
  name: string;
  from: Moment;
  to: Moment;
  selectedCountries: number[] = [];
  selectedTypes: number[] = [];
  selectedYears: number[] = [];
  includeLineColours: boolean;
}
