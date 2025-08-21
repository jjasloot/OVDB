import { Component, OnInit, inject } from "@angular/core";
import { RequestForAdmin } from "src/app/models/requests.model";
import { RequestsService } from "src/app/services/requests.service";
import { MatCard } from "@angular/material/card";
import { MatInput } from "@angular/material/input";
import { FormsModule } from "@angular/forms";
import { MatButton } from "@angular/material/button";
import { DatePipe } from "@angular/common";

@Component({
    selector: "app-administrator-requests",
    templateUrl: "./administrator-requests.component.html",
    styleUrl: "./administrator-requests.component.scss",
    imports: [
        MatCard,
        MatInput,
        FormsModule,
        MatButton,
        DatePipe,
    ]
})
export class AdministratorRequestsComponent implements OnInit {
  private requestsService = inject(RequestsService);

  requests: RequestForAdmin[] = [];
  requestsWithoutResponse = new Set<number>();

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
