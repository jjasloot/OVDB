import { Component, OnInit } from "@angular/core";
import { RequestForAdmin } from "src/app/models/requests.model";
import { RequestsService } from "src/app/services/requests.service";

@Component({
  selector: "app-administrator-requests",
  templateUrl: "./administrator-requests.component.html",
  styleUrl: "./administrator-requests.component.scss",
})
export class AdministratorRequestsComponent implements OnInit {
  requests: RequestForAdmin[] = [];
  requestsWithoutResponse = new Set<number>();
  constructor(private requestsService: RequestsService) {}

  ngOnInit(): void {
    this.getData();
  }

  sendResponse(request: RequestForAdmin) {
    this.requestsService
      .respondToRequest(request.id, { message: request.response })
      .subscribe(() => this.getData());
  }

  private getData(): void {
    this.requestsService.getAdminRequests().subscribe((requests) => {
      this.requests = requests;
      this.requestsWithoutResponse.clear();
      this.requests.forEach((r) => {
        if (!r.response) this.requestsWithoutResponse.add(r.id);
      });
    });
  }
}
