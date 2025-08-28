export interface TripFrequencyData {
  date: string;
  tripCount: number;
  totalDistance: number;
}

export interface ActivityDataPoint {
  date: string;
  tripCount: number;
  totalDistance: number;
  level: number; // 0-4 for heatmap intensity
}

export interface ActivityHeatmapData {
  year: number;
  data: ActivityDataPoint[];
}

export interface RouteRankingData {
  routeId: number;
  routeName: string;
  routeType: string;
  routeTypeNameNL: string;
  tripCount: number;
  totalDistance: number;
  averageDistance: number;
  firstTrip: string;
  lastTrip: string;
}

export interface MonthlyTrendData {
  month: string;
  averageDuration: number;
  tripCount: number;
}

export interface HourlyTrendData {
  hour: number;
  averageDuration: number;
  tripCount: number;
}

export interface RouteTypeTrendData {
  routeType: string;
  routeTypeNL: string;
  averageDuration: number;
  averageSpeed: number;
  tripCount: number;
}

export interface TravelTimeTrendsData {
  averageDurationByMonth: MonthlyTrendData[];
  averageDurationByHour: HourlyTrendData[];
  averageDurationByRouteType: RouteTypeTrendData[];
}

export interface SourceBreakdownData {
  source: string;
  count: number;
}

export interface MonthlyImportData {
  month: string;
  importedCount: number;
}

export interface IntegrationStatsData {
  totalTrips: number;
  tripsWithTiming: number;
  traewellingImported: number;
  tripsWithSource: number;
  timingCompleteness: number;
  sourceBreakdown: SourceBreakdownData[];
  monthlyImports: MonthlyImportData[];
}

export interface RouteTypeBreakdownData {
  routeType: string;
  routeTypeNL: string;
  uniqueRoutes: number;
  tripCount: number;
  totalDistance: number;
}

export interface CoverageOverviewData {
  uniqueRoutes: number;
  totalDistance: number;
  visitedStations: number;
  routeTypeBreakdown: RouteTypeBreakdownData[];
  coverageNote: string;
}