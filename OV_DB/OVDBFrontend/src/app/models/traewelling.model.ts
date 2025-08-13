export interface TrawellingConnectionStatus {
  connected: boolean;
  user?: TrawellingUser;
}

export interface TrawellingUser {
  id: number;
  displayName: string;
  username: string;
  profilePicture?: string;
}

export interface TrawellingConnectResponse {
  authorizationUrl: string;
}

export interface TrawellingOAuthRequest {
  code: string;
  state: string;
}

export interface TrawellingTrip {
  id: number;
  train?: TrawellingTrain;
  origin: TrawellingStation;
  destination: TrawellingStation;
  distance?: number;
  duration?: number;
  points?: number;
  departure: string;
  arrival: string;
  body?: string;
  visibility: number;
  businessCheck?: TrawellingBusinessCheck;
  eventId?: number;
  likes: number;
  liked: boolean;
  isLikeable: boolean;
  statusText?: string;
  travelReason?: number;
  alsoOnThisConnection: TrawellingOtherUser[];
  event?: TrawellingEvent;
  preventIndex: boolean;
  travelType: string;
  category: number;
  isHistoric: boolean;
}

export interface TrawellingTrain {
  trip: string;
  hafasId: string;
  category: string;
  number: string;
  lineName: string;
  journeyNumber?: number;
  distance?: number;
  duration?: number;
  origin: TrawellingStation;
  destination: TrawellingStation;
  stopovers: TrawellingStopover[];
}

export interface TrawellingStation {
  id: number;
  name: string;
  latitude?: number;
  longitude?: number;
  ibnr?: number;
  rilIdentifier?: string;
}

export interface TrawellingStopover {
  id: number;
  name: string;
  evaIdentifier: string;
  rilIdentifier?: string;
  arrival?: string;
  arrivalPlanned?: string;
  arrivalReal?: string;
  departure?: string;
  departurePlanned?: string;
  departureReal?: string;
  isArrivalDelayed?: boolean;
  isDepartureDelayed?: boolean;
  platform?: string;
  platformPlanned?: string;
  isAdditional?: boolean;
  isCancelled?: boolean;
}

export interface TrawellingBusinessCheck {
  id: number;
  name: string;
  slug: string;
}

export interface TrawellingOtherUser {
  id: number;
  displayName: string;
  username: string;
  profilePicture?: string;
  points: number;
  trainDistance: number;
  trainDuration: number;
}

export interface TrawellingEvent {
  id: number;
  name: string;
  slug: string;
  hashtag?: string;
  host?: string;
  url?: string;
  trainstation: TrawellingStation;
  begin: string;
  end: string;
}

export interface TrawellingUnimportedResponse {
  data: TrawellingTrip[];
  meta: TrawellingPaginationMeta;
}

export interface TrawellingPaginationMeta {
  current_page: number;
  from: number;
  last_page: number;
  per_page: number;
  to: number;
  total: number;
}

export interface TrawellingImportRequest {
  statusId: number;
  createRoute?: boolean;
}

export interface TrawellingImportResponse {
  success: boolean;
  routeInstance?: any;
  route?: any;
  message?: string;
}

export interface TrawellingStats {
  totalTrips: number;
  importedTrips: number;
  unimportedTrips: number;
  enhancedInstances: number;
  connectedSince?: string;
}

export interface TrawellingProcessBacklogRequest {
  startDate?: string;
  endDate?: string;
  maxItems?: number;
}

export interface TrawellingProcessBacklogResponse {
  success: boolean;
  imported: number;
  enhanced: number;
  errors: number;
  message: string;
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
}