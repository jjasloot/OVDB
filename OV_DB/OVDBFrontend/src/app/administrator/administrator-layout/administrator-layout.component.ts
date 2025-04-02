import { Component, OnInit } from "@angular/core";
import { Router, RouterLinkActive, RouterLink, RouterOutlet } from "@angular/router";
import { TranslateService } from "@ngx-translate/core";
import { MatTabNav, MatTabLink } from "@angular/material/tabs";
import { SignalRService } from "src/app/services/signal-r.service";

@Component({
    selector: "app-administrator-layout",
    templateUrl: "./administrator-layout.component.html",
    styleUrls: ["./administrator-layout.component.scss"],
    imports: [
        MatTabNav,
        MatTabLink,
        RouterLinkActive,
        RouterLink,
        RouterOutlet,
    ]
})
export class AdministratorLayoutComponent implements OnInit {
  navLinks: { label: string; link: string; index: number }[];
  activeLinkIndex: number;

  constructor(
    private router: Router,
    private translateService: TranslateService,
    private signalRServicve:SignalRService
  ) {
    this.navLinks = [
      {
        label: "Users",
        link: "/administrator/users",
        index: 0,
      },
      {
        label: "Maps",
        link: "/administrator/maps",
        index: 1,
      },
      {
        label: "Stations",
        link: "/administrator/stations",
        index: 2,
      },
      {
        label: "Regions",
        link: "/administrator/regions",
        index: 3,
      },
      {
        label: "Requests",
        link: "/administrator/requests",
        index: 4,
      },
      {
        label: "Operators",
        link: "/administrator/operators",
        index: 5,
      },
    ];
  }
  ngOnInit(): void {
    this.signalRServicve.connect();
    this.router.events.subscribe((res) => {
      this.activeLinkIndex = this.navLinks.indexOf(
        this.navLinks.find((tab) => tab.link === "." + this.router.url)
      );
    });
    // this.translateService.onLangChange.subscribe(() => {
    //   this.navLinks = [
    //     {
    //       label: 'Users',
    //       link: '/administrator/users',
    //       index: 0
    //     }, {
    //       label: 'Maps',
    //       link: '/administrator/maps',
    //       index: 1
    //     }

    //   ];
    // })
  }
}
