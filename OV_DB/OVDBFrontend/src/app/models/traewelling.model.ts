// Träwelling API models based on official API documentation
// https://traewelling.de/api/v1

// Connection and OAuth models
export interface TrawellingConnectionStatus {
  connected: boolean;
  user?: TrawellingUserAuth;
}

export interface TrawellingUserAuth {
  id: number;
  displayName: string;
  username: string;
  profilePicture: string;
  totalDistance: number;
  totalDuration: number;
  points: number;
  mastodonUrl?: string;
  privateProfile: boolean;
  preventIndex: boolean;
  likes_enabled: boolean;
  mapProvider: string;
  home?: TrawellingStation;
  language: string;
  defaultStatusVisibility: number;
  roles: string[];
}

export interface TrawellingConnectResponse {
  authorizationUrl: string;
}

// Status/Trip models
export interface TrawellingStatus {
  id: number;
  body?: string;
  bodyMentions: TrawellingMention[];
  business: TrawellingBusiness;
  visibility: TrawellingStatusVisibility;
  likes: number;
  liked: boolean;
  isLikable: boolean;
  client: TrawellingClient;
  createdAt: string;
  train: TrawellingTransport;
  event?: TrawellingEvent;
  userDetails: TrawellingLightUser;
  tags: TrawellingStatusTag[];
}

export interface TrawellingMention {
  // Define if needed - not fully specified in API docs
  username: string;
  displayName: string;
}

export enum TrawellingBusiness {
  PRIVATE = 0,
  BUSINESS = 1
}

export enum TrawellingStatusVisibility {
  PUBLIC = 0,
  UNLISTED = 1,
  FOLLOWERS = 2,
  PRIVATE = 3
}

export interface TrawellingClient {
  id: number;
  name: string;
  privacyPolicyUrl: string;
}

export interface TrawellingTransport {
  trip: number;
  hafasId: string;
  category: TrawellingHafasTravelType;
  number: string;
  lineName: string;
  journeyNumber: number | null;
  manualJourneyNumber?: string;
  distance: number;
  points: number;
  duration: number;
  manualDeparture?: string;
  manualArrival?: string;
  origin: TrawellingStopover;
  destination: TrawellingStopover;
  operator?: TrawellingOperator;
  dataSource?: TrawellingDataSource;
}

export enum TrawellingHafasTravelType {
  NATIONAL_EXPRESS = 'NationalExpress',
  NATIONAL = 'National',
  REGIONAL_EXP = 'RegionalExp',
  REGIONAL = 'Regional',
  SUBURBAN = 'Suburban',
  BUS = 'Bus',
  FERRY = 'Ferry',
  SUBWAY = 'Subway',
  TRAM = 'Tram',
  TAXI = 'Taxi',
  PLANE = 'Plane'
}

export interface TrawellingStopover {
  id: number;
  name: string;
  rilIdentifier?: string;
  evaIdentifier?: string;
  arrival?: string;
  arrivalPlanned?: string;
  arrivalReal?: string;
  arrivalPlatformPlanned?: string;
  arrivalPlatformReal?: string;
  departure?: string;
  departurePlanned?: string;
  departureReal?: string;
  departurePlatformPlanned?: string;
  departurePlatformReal?: string;
  platform?: string;
  isArrivalDelayed: boolean;
  isDepartureDelayed: boolean;
  cancelled: boolean;
}

export interface TrawellingOperator {
  id: number;
  identifier?: string;
  name: string;
}

export interface TrawellingDataSource {
  id: string;
  attribution: string;
}

export interface TrawellingEvent {
  id: number;
  name: string;
  slug: string;
  hashtag?: string;
  host?: string;
  url?: string;
  begin: string;
  end: string;
  station: TrawellingStation;
  isPride: boolean;
}

export interface TrawellingLightUser {
  id: number;
  displayName: string;
  username: string;
  profilePicture: string;
  mastodonUrl?: string;
  preventIndex: boolean;
}

export interface TrawellingStatusTag {
  key: string;
  value: string;
  visibility: number;
}

export interface TrawellingStation {
  id: number;
  name: string;
  latitude?: number;
  longitude?: number;
  ibnr?: string;
  rilIdentifier?: string;
  areas?: TrawellingArea[];
}

export interface TrawellingArea {
  name: string;
  default: boolean;
  adminLevel: number;
}

// Response wrappers
export interface TrawellingStatusesResponse {
  data: TrawellingStatus[];
  links: TrawellingPaginationLinks;
  meta: TrawellingPaginationMeta;
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

// OVDB specific models for integration
export interface TrawellingStats {
  totalTrips: number;
  importedTripsCount: number;
  unimportedTripsCount: number;
  enhancedInstancesCount: number;
  connectedSince?: string;
}

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
  trawellingStatusId?: number;
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

export interface TrawellingIgnoreRequest {
  statusId: number;
}

export interface TrawellingIgnoreResponse {
  success: boolean;
  message?: string;
}

// Legacy aliases for backward compatibility (will be removed)
export type TrawellingTrip = TrawellingStatus;
export type TrawellingTrain = TrawellingTransport;
export type TrawellingUnimportedResponse = TrawellingStatusesResponse;