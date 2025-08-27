// Clean Träwelling models matching backend API exactly
// Updated with correct endpoint mappings and proper TypeScript types

// Connection status
export interface TrawellingConnectionStatus {
  connected: boolean;
  user?: TrawellingUser;
}

export interface TrawellingUser {
  id: number;
  displayName: string;
  username: string;
  profilePicture: string;
  totalDistance: number;
  totalDuration: number;
  points: number;
}

// Trip models (optimized DTOs from backend)
export interface TrawellingTripsResponse {
  data: TrawellingTrip[];
  links: TrawellingPaginationLinks;
  meta: TrawellingPaginationMeta;
  hasMorePages: boolean;
}

export interface TrawellingTrip {
  id: number;
  body?: string;
  createdAt: string;
  transport: TrawellingTransport;
  tags: TrawellingTag[];
}

export interface TrawellingTransport {
  category: TrawellingTransportCategory;
  number: string;
  lineName: string;
  journeyNumber?: string;
  distance: number;
  duration: number;
  origin: TrawellingStopover;
  destination: TrawellingStopover;
  operator?: TrawellingOperator;
}

export enum TrawellingTransportCategory {
  BUS = 'Bus',
  NATIONAL = 'National',
  NATIONAL_EXPRESS = 'NationalExpress',
  REGIONAL = 'Regional',
  REGIONAL_EXP = 'RegionalExp',
  SUBURBAN = 'Suburban',
  SUBWAY = 'Subway',
  TRAM = 'Tram',
  FERRY = 'Ferry',
  TAXI = 'Taxi',
  PLANE = 'Plane'
}

export interface TrawellingStopover {
  id: number;
  name: string;
  // Scheduled times (local timezone)
  arrivalScheduled?: string;
  departureScheduled?: string;
  // Real/actual times (local timezone)
  arrivalReal?: string;
  departureReal?: string;
  // Status indicators
  isArrivalDelayed: boolean;
  isDepartureDelayed: boolean;
  cancelled: boolean;
}

export interface TrawellingOperator {
  name: string;
  identifier?: string;
}

export interface TrawellingTag {
  key: string;
  value: string;
}

export interface TrawellingPaginationLinks {
  first?: string;
  last?: string;
  prev?: string;
  next?: string;
}

export interface TrawellingPaginationMeta {
  current_page: number;
  from: number;
  path: string;
  per_page: number;
  to: number;
  total?: number;
}

// Route Instance integration
export interface RouteInstanceSearchResult {
  id: number;
  routeId: number;
  routeName: string;
  from: string;
  to: string;
  date: string;
  startTime?: string;
  endTime?: string;
  durationHours?: number;
  hasTraewellingLink: boolean;
}

export interface LinkToRouteInstanceRequest {
  statusId: number;
  routeInstanceId: number;
}

export interface LinkToRouteInstanceResponse {
  success: boolean;
  routeInstance?: RouteInstanceSearchResult;
  message?: string;
}

// Ignore functionality
export interface TrawellingIgnoreRequest {
  statusId: number;
}

export interface TrawellingIgnoreResponse {
  success: boolean;
  message?: string;
}

export interface RoutesListResponse {
  count: number;
  routes: RouteSearchResult[];
}

// Route search for "Add to Existing Route" functionality
export interface RouteSearchResult {
  routeId: number;
  name: string;
  from: string;
  to: string;
  lineNumber?: string;
  firstDateTime: Date | string | null;
  operatingCompany?: string;
  routeType?: {
    name: string;
    nameNL: string;
    colour?: string;
  };
}

// Connection responses
export interface TrawellingConnectResponse {
  authorizationUrl: string;
}

// Legacy models for backward compatibility
export interface TrawellingStatusesResponse {
  data: any[];
  links: TrawellingPaginationLinks;
  meta: TrawellingPaginationMeta;
}

export interface TrawellingStats {
  totalTrips: number;
  importedTripsCount: number;
  unimportedTripsCount: number;
  enhancedInstancesCount: number;
  connectedSince?: string;
}

// Trip context for route creation workflows
export interface TrawellingTripContext {
  tripId: number;
  originName: string;
  destinationName: string;
  departureTime?: string;
  arrivalTime?: string;
  transportCategory: TrawellingTransportCategory;
  lineNumber: string;
  journeyNumber?: string;
  distance: number;
  duration: number;
  description?: string;
  tags: TrawellingTag[];
  date: string;
}