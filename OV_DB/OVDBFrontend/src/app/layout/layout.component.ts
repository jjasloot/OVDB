import { Component, OnInit } from "@angular/core";
import { Router, RouterLink, RouterOutlet } from "@angular/router";
import { AuthenticationService } from "../services/authentication.service";
import { TranslationService } from "../services/translation.service";
import { MatSelectChange, MatSelect } from "@angular/material/select";
import { RequestsService } from "../services/requests.service";
import { MatToolbar } from "@angular/material/toolbar";
import { MatIconButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import { MatOption } from "@angular/material/core";

@Component({
    selector: "app-layout",
    templateUrl: "./layout.component.html",
    styleUrls: ["./layout.component.scss"],
    imports: [
        MatToolbar,
        MatIconButton,
        RouterLink,
        MatIcon,
        MatSelect,
        MatOption,
        RouterOutlet,
    ]
})
export class LayoutComponent implements OnInit {
  hasUnreadRequests: boolean = false;
  hasUnreadRequestsAdmin: boolean = false;
  constructor(
    private router: Router,
    private authService: AuthenticationService,
    private requestsService: RequestsService,
    private translationService: TranslationService
  ) {}

  ngOnInit() {
    if (this.isLoggedIn) {
      this.requestsService.hasAnyUnreadRequests().subscribe((hasUnread) => {
        this.hasUnreadRequests = hasUnread;
      });
      if (this.isAdmin) {
        this.requestsService
          .adminHasAnyUnreadRequests()
          .subscribe((hasUnread) => {
            this.hasUnreadRequestsAdmin = hasUnread;
          });
      }
    }
  }
  gotoHome() {
    this.router.navigate(["/"]);
  }

  setLanguage(language: MatSelectChange) {
    this.translationService.language = language.value;
  }
  get currentLanguage() {
    return this.translationService.language;
  }
  signOut() {
    this.authService.logOut();
  }
  signIn() {
    this.router.navigate(["/login"]);
  }

  get isLoggedIn() {
    return this.authService.isLoggedIn;
  }
  get email() {
    if (!this.isLoggedIn) {
      return "";
    }
    return this.authService.email;
  }

  get isAdmin() {
    if (!this.isLoggedIn) {
      return false;
    }
    return this.authService.admin;
  }
}
