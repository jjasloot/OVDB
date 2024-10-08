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
  connect() {
    const connection = new HubConnectionBuilder()
      .withUrl(environment.backend + "mapGenerationHub")
      .build();
    connection
      .start()
      .then(() => (this.connected = true))
      .catch((err) => console.error(err.toString()));
    this.connection = connection;
    connection.on("GenerationUpdate", (requestIdentifier, percentage) => {
      this.updates$.next({ requestIdentifier, percentage });
    });
    connection.onclose(() => (this.connected = false));
  }

  disconnect() {
    this.connection?.stop();
  }
}
