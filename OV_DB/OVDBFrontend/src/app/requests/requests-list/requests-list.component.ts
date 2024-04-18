import { Component, OnInit } from "@angular/core";
import { RequestForUser } from "src/app/models/requests.model";
import { RequestsService } from "src/app/services/requests.service";
import { TranslationService } from "src/app/services/translation.service";

@Component({
  selector: "app-requests-list",
  templateUrl: "./requests-list.component.html",
  styleUrl: "./requests-list.component.scss",
})
export class RequestsListComponent implements OnInit {
  requests: RequestForUser[];
  newRequest?: string;
  constructor(
    private requestsService: RequestsService,
    private translationService: TranslationService
  ) {}

  ngOnInit(): void {
    this.requestsService.getUserRequests().subscribe((requests) => {
      this.requests = requests;
    });
  }

  sendRequest() {
    this.requestsService.addNewRequest({ message: this.newRequest }).subscribe({
      next: () => {
        this.newRequest = "";
        this.requestsService.getUserRequests().subscribe((requests) => {
          this.requests = requests;
        });
      },
    });
  }

  get currentLocale() {
    return this.translationService.dateLocale;
  }
}
