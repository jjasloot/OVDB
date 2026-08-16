export interface DelayBucket {
  key: string;
  count: number;
}

export interface GroupPunctuality {
  label: string;
  trips: number;
  averageArrivalDelayMinutes: number;
  onTimePercentage: number;
}

export interface DelayedTrip {
  routeInstanceId: number;
  routeId: number;
  date: string;
  name: string;
  nameNL: string;
  operator: string;
  delayMinutes: number;
}

export interface PunctualityStats {
  totalTrips: number;
  tripsWithDepartureData: number;
  tripsWithArrivalData: number;
  averageDepartureDelayMinutes: number | null;
  averageArrivalDelayMinutes: number | null;
  medianArrivalDelayMinutes: number | null;
  onTimeThresholdMinutes: number;
  onTimePercentage: number | null;
  arrivalDelayDistribution: DelayBucket[];
  byOperator: GroupPunctuality[];
  byYear: GroupPunctuality[];
  worstTrips: DelayedTrip[];
}
