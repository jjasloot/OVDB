import { environment } from "src/environments/environment";
import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { Injectable } from "@angular/core";
import { Subject } from "rxjs";

@Injectable({
  providedIn: "root",
})
export class SignalRService {
  private connection?: HubConnection;
  public connected = false;
  updates$ = new Subject<{ requestIdentifier: string; percentage: number }>();
  regionUpdates$ = new Subject<{ regionId: number; percentage: number, updatedRoutes: number | null }>();
  stationUpdates$ = new Subject<{ regionId: number; percentage: number }>();
  connect() {
    if (this.connected) {
      return;
    }
    const connection = new HubConnectionBuilder()
      .withUrl(environment.backend + "mapGenerationHub")
      .withAutomaticReconnect()
      .build();
    connection
      .start()
      .then(() => (this.connected = true))
      .catch((err) => console.error(err.toString()));
    this.connection = connection;
    connection.on("GenerationUpdate", (requestIdentifier, percentage) => {
      this.updates$.next({ requestIdentifier, percentage });
    });
    connection.on("RefreshRoutes", (regionId, percentage, updatedRoutes) => {
      this.regionUpdates$.next({ regionId, percentage, updatedRoutes });
    });
    connection.on("RefreshStations", (regionId, percentage) => {
      this.stationUpdates$.next({ regionId, percentage });
      console.log(regionId + ": " + percentage);
    });
    connection.onclose(() => (this.connected = false));
  }

  disconnect() {
    this.connection?.stop();
  }
}
