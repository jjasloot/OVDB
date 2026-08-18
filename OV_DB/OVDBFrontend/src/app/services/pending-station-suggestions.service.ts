import { Injectable } from "@angular/core";
import { OSMStop } from "../models/stationView.model";

/**
 * Holds the stops an OSM import parsed out of a relation until there is a date to attach them to.
 *
 * The wizard has the stops but the route has no trip yet, so asking "did you get off here?" then is
 * premature — the answer could not be dated. The stops wait here across the navigation to the route
 * form and, if the user goes on to add a trip, across that too.
 *
 * Deliberately in memory only: a calling pattern is a fact about an import, not something to store
 * and let go stale. A page reload drops it, which is the right trade for keeping the model clean.
 */
@Injectable({ providedIn: "root" })
export class PendingStationSuggestionsService {
  private byRouteId = new Map<number, OSMStop[]>();

  set(routeId: number, stops: OSMStop[] | undefined): void {
    if (stops?.length) {
      this.byRouteId.set(routeId, stops);
    }
  }

  /** Reads without consuming, for a step that may hand on to a later one. */
  peek(routeId: number): OSMStop[] | undefined {
    return this.byRouteId.get(routeId);
  }

  /** Reads and clears, so one import cannot be offered twice. */
  take(routeId: number): OSMStop[] | undefined {
    const stops = this.byRouteId.get(routeId);
    this.byRouteId.delete(routeId);
    return stops;
  }
}
