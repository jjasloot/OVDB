import { environment } from "src/environments/environment";
import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { Injectable } from "@angular/core";
import { Subject } from "rxjs";

@Injectable({
  providedIn: "root",
})
export class SignalRService {
  private connection?: HubConnection;
  private startPromise?: Promise<void>;
  public connected = false;
  updates$ = new Subject<{ requestIdentifier: string; percentage: number }>();
  regionUpdates$ = new Subject<{ regionId: number; percentage: number, updatedRoutes: number | null }>();
  stationUpdates$ = new Subject<{ regionId: number; percentage: number }>();
  connect() {
    if (this.connection) {
      return;
    }
    const connection = new HubConnectionBuilder()
      .withUrl(environment.backend + "mapGenerationHub")
      .withAutomaticReconnect()
      .build();
    this.connection = connection;
    connection.on("GenerationUpdate", (requestIdentifier, percentage) => {
      this.updates$.next({ requestIdentifier, percentage });
    });
    connection.on("RefreshRoutes", (regionId, percentage, updatedRoutes) => {
      this.regionUpdates$.next({ regionId, percentage, updatedRoutes });
    });
    connection.on("RefreshStations", (regionId, percentage) => {
      this.stationUpdates$.next({ regionId, percentage });
    });
    connection.onclose(() => (this.connected = false));
    this.startPromise = connection
      .start()
      .then(() => { this.connected = true; })
      .catch((err) => console.error(err.toString()));
  }

  // Join a per-request group so map-generation progress is delivered only to the client that
  // requested it, instead of broadcast to everyone. Must complete before the generation request
  // is issued so no early progress events are missed.
  async joinGenerationGroup(requestIdentifier: string): Promise<void> {
    try {
      this.connect();
      await this.startPromise;
      await this.connection?.invoke("JoinGenerationGroup", requestIdentifier);
    } catch (err) {
      console.error("Failed to join generation group", err);
    }
  }

  disconnect() {
    this.connection?.stop();
    this.connected = false;
    this.connection = undefined;
    this.startPromise = undefined;
  }
}
