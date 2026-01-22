import { Component, OnInit, effect, inject, signal } from "@angular/core";
import { Router, RouterLinkActive, RouterLink, RouterOutlet } from "@angular/router";
import { TranslateService } from "@ngx-translate/core";
import { MatTabNav, MatTabLink, MatTabNavPanel } from "@angular/material/tabs";
import { UserPreferenceService } from "src/app/services/user-preference.service";
import { toSignal } from "@angular/core/rxjs-interop";

@Component({
  selector: "app-routes",
  templateUrl: "./routes.component.html",
  styleUrls: ["./routes.component.scss"],
  imports: [
    MatTabNav,
    MatTabLink,
    RouterLinkActive,
    RouterLink,
    MatTabNavPanel,
    RouterOutlet,
  ]
})
export class RoutesComponent implements OnInit {
  private router = inject(Router);
  private translateService = inject(TranslateService);
  private userPreferenceService = inject(UserPreferenceService);
  navLinks = signal<{ label: string; link: string; index: number }[]>([]);
  activeLinkIndex: number;

  language = toSignal(this.translateService.onLangChange);

  menuEffect = effect(() => {
    this.language();
    this.userPreferenceService.hasTraewelling();
    const navLinks = [
      {
        label: this.translateService.instant("ADMINPAGES.MAPS"),
        link: "/admin/maps",
        index: 0,
      },
      {
        label: this.translateService.instant("ADMINPAGES.STATIONMAPS"),
        link: "/admin/stationmaps",
        index: 1,
      },
      {
        label: this.translateService.instant("ADMINPAGES.ROUTES"),
        link: "/admin/routes",
        index: 2,
      },
      {
        label: this.translateService.instant("ADMINPAGES.ADDROUTE"),
        link: "/admin/addRoute",
        index: 3,
      },
      {
        label: this.translateService.instant("ADMINPAGES.WIZARD"),
        link: "/admin/wizard",
        index: 4,
      },
      {
        label: this.translateService.instant(
          "ADMINPAGES.ROUTESMISSINGINFORMATION"
        ),
        link: "/admin/missingData",
        index: 5,
      },
      {
        label: this.translateService.instant("ADMINPAGES.TYPES"),
        link: "/admin/types",
        index: 7,
      },
    ];
    if (this.userPreferenceService.hasTraewelling()) {
      navLinks.push({
        label: this.translateService.instant("ADMINPAGES.TRAEWELLING"),
        link: "/admin/traewelling",
        index: 6,
      });
    }

    this.navLinks.set(navLinks);
  });

  ngOnInit(): void {
    this.router.events.subscribe((res) => {
      this.activeLinkIndex = this.navLinks().indexOf(
        this.navLinks().find((tab) => tab.link === "." + this.router.url)
      );
    });

  }
}
