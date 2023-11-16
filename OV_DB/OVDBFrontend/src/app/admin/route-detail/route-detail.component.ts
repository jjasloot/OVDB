import { Component, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Route } from 'src/app/models/route.model';
import { ApiService } from 'src/app/services/api.service';
import { UntypedFormBuilder, Validators, UntypedFormGroup } from '@angular/forms';
import * as moment from 'moment';
import { RouteType } from 'src/app/models/routeType.model';
import { Location } from '@angular/common';
import { Country } from 'src/app/models/country.model';
import { MatSelectionList } from '@angular/material/list';
import { UpdateRoute } from 'src/app/models/updateRoute.model';
import { Map } from 'src/app/models/map.model';
import { TranslateService } from '@ngx-translate/core';
import { DateAdapter } from '@angular/material/core';
import { TranslationService } from 'src/app/services/translation.service';
import { MatDialog } from '@angular/material/dialog';
import { AreYouSureDialogComponent } from 'src/app/are-you-sure-dialog/are-you-sure-dialog.component';

@Component({
  selector: 'app-route-detail',
  templateUrl: './route-detail.component.html',
  styleUrls: ['./route-detail.component.scss']
})
export class RouteDetailComponent implements OnInit {
  routeId: number;
  route: Route;
  form: UntypedFormGroup;
  types: RouteType[];
  countries: Country[];
  maps: Map[];
  colour: string;

  @ViewChild('countriesSelection') countriesSelection: MatSelectionList;
  @ViewChild('mapsSelection') mapsSelection: MatSelectionList;

  selectedOptions: number[];
  selectedMaps: number[];

  constructor(
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private formBuilder: UntypedFormBuilder,
    private dateAdapter: DateAdapter<any>,
    private dialog: MatDialog,
    private router: Router) {
    this.dateAdapter.setLocale(this.translationService.dateLocale);

    this.form = this.formBuilder.group({
      name: ['', Validators.required],
      nameNL: '',
      description: '',
      descriptionNL: '',
      from: '',
      to: '',
      lineNumber: null,
      operatingCompany: null,
      overrideColour: null,
      firstDateTime: '',
      routeTypeId: [null, Validators.required],
      calculatedDistance: [null],
      overrideDistance: [null]
    });
  }

  ngOnInit() {
    this.translationService.languageChanged.subscribe(() => {
      this.sortOrder();
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.apiService.getTypes().subscribe(types => {
      this.types = types;
    });
    this.apiService.getCountries().subscribe(countries => {
      this.countries = countries;
      this.sortOrder();
    });
    this.apiService.getMaps().subscribe(maps => {
      this.maps = maps;
    });
    this.activatedRoute.paramMap.subscribe(p => {
      this.routeId = +p.get('routeId');
      this.apiService.getRoute(this.routeId).subscribe(data => {
        this.route = data;
        if (!this.route.firstDateTime) {
          this.route.firstDateTime = moment();
        }
        this.colour = this.route.overrideColour;
        this.selectedOptions = this.route.routeCountries.map(r => r.countryId);
        this.selectedMaps = this.route.routeMaps.map(r => r.mapId);
        this.form.patchValue(this.route);
        if (this.route.routeInstancesCount > 1) {
          this.form.controls.firstDateTime.disable();
        }
      });
    });
  }
  sortOrder() {
    this.countries = this.countries.sort((a, b) => {
      if (this.name(a) > this.name(b)) {
        return 1;
      }
      if (this.name(a) < this.name(b)) {
        return -1;
      }
      return 0;
    });
  }

  onSubmit(values) {
    if (this.form.invalid) {
      return;
    }
    if (this.mapsSelection.selectedOptions.selected.length === 0) {
      return false;
    }
    const route = values as UpdateRoute;
    route.routeId = this.route.routeId;
    route.overrideColour = this.colour;
    route.countries = this.countriesSelection.selectedOptions.selected.map(s => s.value);
    route.maps = this.mapsSelection.selectedOptions.selected.map(s => s.value);
    this.apiService.updateRoute(values as Route).subscribe(_ => this.goBack());
  }


  goBack(): void {
    this.router.navigate(['/', 'admin', 'routes']);
  }
  delete() {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      width: this.getWidth(),
      data: {
        item: this.translateService.instant('ROUTE.DELETEFRONT') + ' ' + this.route.name + ' ' + this.translateService.instant('ROUTE.DELETEBACK')
      }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.apiService.deleteRoute(this.route.routeId).subscribe(_ => {
          this.goBack();
        });
      }
    });
  }

  get countriesString() {
    if (!this.countriesSelection || !this.countries) {
      return '';
    }
    const countries = this.countries
      .filter(c => this.countriesSelection.selectedOptions.selected.some(rc => rc.value === c.countryId))
      .map(c => this.name(c));
    if (countries.length > 3) {
      return '' + countries.length + ' ' + this.translateService.instant('ROUTEDETAILS.COUNTRIESINSTRING');
    }
    return countries.join(', ');
  }

  get mapsString() {
    if (!this.mapsSelection || !this.maps) {
      return '';
    }
    const maps = this.maps
      .filter(m => this.mapsSelection.selectedOptions.selected.some(rm => rm.value === m.mapId))
      .map(m => this.name(m));
    if (maps.length > 3) {
      return '' + maps.length + ' ' + this.translateService.instant('ROUTEDETAILS.MAPSINSTRING');
    }
    return maps.join(', ');
  }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }

  export() {
    this.apiService.getExport(this.route.routeId).subscribe(data => {
      saveAs(data, this.route.name.trim().replace(' ', '_') + '.kml');
    });
  }

  private getWidth() {
    let width = '90%';
    if (window.innerWidth > 600) {
      width = '50%';
    }
    return width;
  }
}
