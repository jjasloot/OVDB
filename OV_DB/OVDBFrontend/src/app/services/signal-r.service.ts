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
  private connecting = false;
  updates$ = new Subject<{ requestIdentifier: string; percentage: number }>();
  regionUpdates$ = new Subject<{ regionId: number; percentage: number, updatedRoutes: number | null }>();
  stationUpdates$ = new Subject<{ regionId: number; percentage: number }>();
  connect() {
    if (this.connected || this.connecting) {
      return;
    }
    this.connecting = true;
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
      console.log(regionId + ": " + percentage);
    });
    connection.onclose(() => {
      this.connected = false;
      this.connecting = false;
    });
    connection
      .start()
      .then(() => {
        this.connected = true;
        this.connecting = false;
      })
      .catch((err) => {
        console.error(err.toString());
        this.connecting = false;
      });
  }

  disconnect() {
    this.connection?.stop();
  }
}
