export interface CountryVisit {
  isoCode: string;
  flagEmoji: string;
  name: string;
  nameNL: string;
}

export interface NameCount {
  name: string;
  nameNL: string;
  trips: number;
  distanceKm: number;
}

export interface HighlightTrip {
  routeId: number;
  date: string;
  name: string;
  nameNL: string;
  distanceKm: number;
  durationHours: number | null;
  averageSpeedKmh: number | null;
}

export interface BusiestDay {
  date: string;
  trips: number;
  distanceKm: number;
}

export interface YearInReview {
  year: number;
  trips: number;
  distanceKm: number;
  durationHours: number;
  activeDays: number;
  distinctRoutes: number;
  newRoutes: number;
  countries: CountryVisit[];
  topRouteTypes: NameCount[];
  topOperators: NameCount[];
  monthlyDistanceKm: number[];
  longestTrip: HighlightTrip | null;
  fastestTrip: HighlightTrip | null;
  busiestDay: BusiestDay | null;
  onTimePercentage: number | null;
  averageArrivalDelayMinutes: number | null;
  tripsWithArrivalData: number;
  previousYearTrips: number;
  previousYearDistanceKm: number;
}
