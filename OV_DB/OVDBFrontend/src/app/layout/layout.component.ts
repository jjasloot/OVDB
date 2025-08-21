import { Component, OnInit, inject } from "@angular/core";
import { Router, RouterLink, RouterOutlet } from "@angular/router";
import { AuthenticationService } from "../services/authentication.service";
import { TranslationService } from "../services/translation.service";
import { ThemeService } from "../services/theme.service";
import { MatSelect } from "@angular/material/select";
import { RequestsService } from "../services/requests.service";
import { MatToolbar } from "@angular/material/toolbar";
import { MatButton, MatIconButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import { MatOption } from "@angular/material/core";
import { MatMenuModule } from "@angular/material/menu";
import { TranslateModule } from "@ngx-translate/core";

@Component({
  selector: "app-layout",
  templateUrl: "./layout.component.html",
  styleUrls: ["./layout.component.scss"],
  imports: [
    MatToolbar,
    MatIconButton,
    MatButton,
    RouterLink,
    MatIcon,
    MatSelect,
    MatOption,
    RouterOutlet,
    MatMenuModule,
    TranslateModule
  ]
})
export class LayoutComponent implements OnInit {
  private router = inject(Router);
  private authService = inject(AuthenticationService);
  private requestsService = inject(RequestsService);
  private translationService = inject(TranslationService);
  themeService = inject(ThemeService);

  hasUnreadRequests = false;
  hasUnreadRequestsAdmin = false;

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

  setLanguage(language: 'nl' | 'en') {
    this.translationService.language = language;
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

  toggleTheme() {
    this.themeService.toggleDarkMode();
  }
}
