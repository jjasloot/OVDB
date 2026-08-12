import { Component, DestroyRef, OnInit, inject, ChangeDetectionStrategy } from "@angular/core";
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { AuthenticationService } from "../services/authentication.service";
import { TranslationService } from "../services/translation.service";
import { ThemeService } from "../services/theme.service";
import { RequestsService } from "../services/requests.service";
import { MatToolbar } from "@angular/material/toolbar";
import { MatButton, MatIconButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import { MatMenuModule } from "@angular/material/menu";
import { MatTooltip } from "@angular/material/tooltip";
import { TranslateModule } from "@ngx-translate/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";

@Component({
  selector: "app-layout",
  templateUrl: "./layout.component.html",
  styleUrls: ["./layout.component.scss"],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    MatToolbar,
    MatIconButton,
    MatButton,
    RouterLink,
    RouterLinkActive,
    MatIcon,
    RouterOutlet,
    MatMenuModule,
    MatTooltip,
    TranslateModule
  ]
})
export class LayoutComponent implements OnInit {
  private router = inject(Router);
  private authService = inject(AuthenticationService);
  private requestsService = inject(RequestsService);
  private translationService = inject(TranslationService);
  private destroyRef = inject(DestroyRef);
  themeService = inject(ThemeService);

  hasUnreadRequests = false;
  hasUnreadRequestsAdmin = false;

  ngOnInit() {
    // Follow the shared flags rather than checking once: opening the requests
    // page marks them read server-side, and the toolbar has to notice.
    this.requestsService.hasUnread$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((hasUnread) => {
        this.hasUnreadRequests = hasUnread;
      });
    this.requestsService.hasUnreadAdmin$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((hasUnread) => {
        this.hasUnreadRequestsAdmin = hasUnread;
      });

    if (this.isLoggedIn) {
      this.requestsService.refreshUnreadRequests();
      if (this.isAdmin) {
        this.requestsService.refreshUnreadAdminRequests();
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
