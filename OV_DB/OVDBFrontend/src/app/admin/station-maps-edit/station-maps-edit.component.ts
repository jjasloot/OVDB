import { Route } from '@angular/compiler/src/core';
import { Component, Inject, OnInit, ViewChild } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { DateAdapter } from '@angular/material/core';
import { MatLegacyDialog as MatDialog, MatLegacyDialogRef as MatDialogRef, MAT_LEGACY_DIALOG_DATA as MAT_DIALOG_DATA } from '@angular/material/legacy-dialog';
import { MatLegacySelectionList as MatSelectionList } from '@angular/material/legacy-list';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import * as moment from 'moment';
import { AreYouSureDialogComponent } from 'src/app/are-you-sure-dialog/are-you-sure-dialog.component';
import { StationCountry } from 'src/app/models/stationCountry.model';
import { StationMap, StationMapCountryDTO } from 'src/app/models/stationMap.model';
import { UpdateRoute } from 'src/app/models/updateRoute.model';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-station-maps-edit',
  templateUrl: './station-maps-edit.component.html',
  styleUrls: ['./station-maps-edit.component.scss']
})
export class StationMapsEditComponent implements OnInit {
  form: UntypedFormGroup;
  countries: StationCountry[];
  @ViewChild('countriesSelection') countriesSelection: MatSelectionList;
  selectedOptions: number[] = [];
  map: StationMap = {} as StationMap;
  constructor(
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private formBuilder: UntypedFormBuilder,
    private dateAdapter: DateAdapter<any>,
    private dialog: MatDialog,
    private router: Router,
    private dialogRef: MatDialogRef<StationMapsEditComponent>,
    @Inject(MAT_DIALOG_DATA) data
  ) {
    if (!!data && !!data.map) {
      this.map = data.map || {} as StationMap;
      this.selectedOptions = this.map.stationMapCountries.map(smc => smc.stationCountryId) || [];
    }
    this.form = this.formBuilder.group({
      name: [this.map?.name ?? '', Validators.required],
      nameNL: this.map?.nameNL ?? '',
      sharingLinkName: this.map?.sharingLinkName ?? ''
    });
  }

  ngOnInit() {
    this.translationService.languageChanged.subscribe(() => {
      this.sortOrder();
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });
    this.apiService.getStationCountries().subscribe(countries => {
      this.countries = countries;
      this.sortOrder();
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
    if (this.form.invalid || this.selectedOptions.length < 1) {
      return;
    }
    this.map.name = this.form.value.name;
    this.map.nameNL = this.form.value.nameNL;
    this.map.sharingLinkName = this.form.value.sharingLinkName;
    this.map.stationMapCountries = this.selectedOptions.map(o => {
      return { includeSpecials: false, stationCountryId: o } as StationMapCountryDTO
    })
    if (!!this.map.stationMapId) {
      this.apiService.updateStationMap(this.map).subscribe(() => {
        this.dialogRef.close(true);
      });
    } else {
      this.apiService.addStationMap(this.map).subscribe(() => {
        this.dialogRef.close(true);
      });
    }

  }


  goBack(): void {
    this.dialogRef.close(false);
  }
  // delete() {
  //   const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
  //     width: this.getWidth(),
  //     data: {
  //       item: this.translateService.instant('ROUTE.DELETEFRONT') + ' ' + this.map.name + ' ' + this.translateService.instant('ROUTE.DELETEBACK')
  //     }
  //   });
  //   dialogRef.afterClosed().subscribe((result: boolean) => {
  //     if (!!result) {
  //       this.apiService.deleteRoute(this.route.routeId).subscribe(_ => {
  //         this.goBack();
  //       });
  //     }
  //   });
  // }

  name(item: any) {
    return this.translationService.getNameForItem(item);
  }

  private getWidth() {
    let width = '90%';
    if (window.innerWidth > 600) {
      width = '50%';
    }
    return width;
  }
}
