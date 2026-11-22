import { DateTime } from "luxon";

/** A trip property condition: the text contains `value`, or for yes/no properties equals `bool`. */
export interface PropertyCondition {
  key: string;
  value?: string;
  bool?: boolean;
}

export class FilterSettings {
  constructor(
    name: string,
    includeLineColours: boolean,
    limitToSelectedAreas: boolean,
    from?: DateTime | null,
    to?: DateTime | null,
    selectedCountries?: number[],
    selectedTypes?: number[],
    selectedYears?: number[],
    selectedOperators?: number[],
    selectedProperties?: PropertyCondition[]
  ) {
    this.name = name;
    this.from = from || null;
    this.to = to || null;
    this.selectedCountries = selectedCountries || [];
    this.selectedTypes = selectedTypes || [];
    this.selectedYears = selectedYears || [];
    this.selectedOperators = selectedOperators || [];
    this.selectedProperties = selectedProperties || [];
    this.includeLineColours = includeLineColours;
    this.limitToSelectedAreas = limitToSelectedAreas;
  }
  name: string;
  from: DateTime | null;
  to: DateTime | null;
  selectedCountries: number[] = [];
  selectedTypes: number[] = [];
  selectedYears: number[] = [];
  selectedOperators: number[] = [];
  selectedProperties: PropertyCondition[] = [];
  includeLineColours: boolean;
  limitToSelectedAreas: boolean;
}
