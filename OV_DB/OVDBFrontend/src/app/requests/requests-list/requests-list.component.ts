import { Component, OnInit, inject, ChangeDetectionStrategy } from "@angular/core";
import { RequestForUser } from "src/app/models/requests.model";
import { RequestsService } from "src/app/services/requests.service";
import { TranslationService } from "src/app/services/translation.service";
import { MatCard, MatCardTitle } from "@angular/material/card";
import { MatFormField, MatLabel } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { FormsModule } from "@angular/forms";
import { MatButton } from "@angular/material/button";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { DatePipe } from "@angular/common";
import { TranslateModule } from "@ngx-translate/core";

@Component({
    selector: "app-requests-list",
    templateUrl: "./requests-list.component.html",
    styleUrl: "./requests-list.component.scss",
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [
        MatCard,
        MatCardTitle,
        MatFormField,
        MatLabel,
        MatInput,
        FormsModule,
        MatButton,
        MatProgressSpinner,
        DatePipe,
        TranslateModule,
    ]
})
export class RequestsListComponent implements OnInit {
  private requestsService = inject(RequestsService);
  private translationService = inject(TranslationService);

  requests!: RequestForUser[];
  newRequest?: string;
  loading = false;
  sending = false;

  ngOnInit(): void {
    this.loadRequests();
  }

  private loadRequests() {
    this.loading = true;
    this.requestsService.getUserRequests().subscribe({
      next: (requests) => {
        this.requests = requests;
        this.loading = false;
      },
      error: () => (this.loading = false),
    });
  }

  sendRequest() {
    if (!this.newRequest || this.newRequest.trim() === "" || this.sending) {
      return;
    }
    this.sending = true;
    this.requestsService.addNewRequest({ message: this.newRequest }).subscribe({
      next: () => {
        this.newRequest = "";
        this.sending = false;
        this.loadRequests();
      },
      error: () => (this.sending = false),
    });
  }

  get currentLocale() {
    return this.translationService.dateLocale;
  }
}
