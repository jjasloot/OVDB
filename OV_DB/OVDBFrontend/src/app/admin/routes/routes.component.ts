import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-routes',
  templateUrl: './routes.component.html',
  styleUrls: ['./routes.component.scss']
})
export class RoutesComponent implements OnInit {
  navLinks: { label: string; link: string; index: number; }[];
  activeLinkIndex: number;

  constructor(private router: Router, private translateService: TranslateService) {
    this.navLinks = [
      {
        label: this.translateService.instant('ADMINPAGES.MAPS'),
        link: '/admin/maps',
        index: 0
      }, {
        label: this.translateService.instant('ADMINPAGES.ROUTES'),
        link: '/admin/routes',
        index: 1
      }, {
        label: this.translateService.instant('ADMINPAGES.ADDROUTE'),
        link: '/admin/addRoute',
        index: 2
      }, {
        label: this.translateService.instant('ADMINPAGES.WIZARD'),
        link: '/admin/wizard',
        index: 3
      }, {
        label: this.translateService.instant('ADMINPAGES.ROUTESMISSINGINFORMATION'),
        link: '/admin/missingData',
        index: 4
      }, {
        label: this.translateService.instant('ADMINPAGES.COUNTRIES'),
        link: '/admin/countries',
        index: 5
      }, {
        label: this.translateService.instant('ADMINPAGES.TYPES'),
        link: '/admin/types',
        index: 6
      }
    ];
  }
  ngOnInit(): void {
    this.router.events.subscribe((res) => {
      this.activeLinkIndex = this.navLinks.indexOf(this.navLinks.find(tab => tab.link === '.' + this.router.url));
    });
    this.translateService.onLangChange.subscribe(() => {
      this.navLinks = [
        {
          label: this.translateService.instant('ADMINPAGES.MAPS'),
          link: '/admin/maps',
          index: 0
        }, {
          label: this.translateService.instant('ADMINPAGES.ROUTES'),
          link: '/admin/routes',
          index: 1
        }, {
          label: this.translateService.instant('ADMINPAGES.ADDROUTE'),
          link: '/admin/addRoute',
          index: 2
        },
        {
          label: this.translateService.instant('ADMINPAGES.WIZARD'),
          link: '/admin/wizard',
          index: 3
        },
        {
          label: this.translateService.instant('ADMINPAGES.ROUTESMISSINGINFORMATION'),
          link: '/admin/missingData',
          index: 4
        }, {
          label: this.translateService.instant('ADMINPAGES.COUNTRIES'),
          link: '/admin/countries',
          index: 5
        }, {
          label: this.translateService.instant('ADMINPAGES.TYPES'),
          link: '/admin/types',
          index: 6
        }

      ];
    });
  }
}
