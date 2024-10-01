import { Component, OnInit } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { Country } from 'src/app/models/country.model';
import { MatDialog } from '@angular/material/dialog';
import { CountryAddComponent } from '../country-add/country-add.component';
import { AreYouSureDialogComponent } from 'src/app/are-you-sure-dialog/are-you-sure-dialog.component';
import { DataUpdateService } from 'src/app/services/data-update.service';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { TranslationService } from 'src/app/services/translation.service';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatList, MatListItem } from '@angular/material/list';
import { MatButton, MatIconButton, MatFabButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';

@Component({
    selector: 'app-countries',
    templateUrl: './countries.component.html',
    styleUrls: ['./countries.component.scss'],
    standalone: true,
    imports: [MatProgressSpinner, MatList, MatListItem, MatButton, MatIconButton, MatIcon, MatFabButton, TranslateModule]
})
export class CountriesComponent implements OnInit {
  data: Country[];
  loading = false;

  constructor(
    private apiService: ApiService,
    private router: Router,
    private dialog: MatDialog,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private dataUpdateService: DataUpdateService) { }

  ngOnInit() {
    this.loadData();
    this.translationService.languageChanged.subscribe(() => this.sort());
  }

  private loadData() {
    this.loading = true;
    this.apiService.getCountries().subscribe(data => {
      this.data = data;
      this.sort();
      this.loading = false;
    });
  }

  sort() {
    this.data = this.data.sort((a, b) => {
      if (this.name(a) > this.name(b)) {
        return 1;
      }
      if (this.name(a) < this.name(b)) {
        return -1;
      }
      return 0;
    });
  }

  name(country: Country) {
    return this.translationService.getNameForItem(country);
  }

  add() {
    const dialogRef = this.dialog.open(CountryAddComponent, {
      width: this.getWidth(),
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.loadData();
      }
    });
  }

  edit(country: Country) {
    const dialogRef = this.dialog.open(CountryAddComponent, {
      width: this.getWidth(),
      data: { country }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.loadData();
      }
    });
  }
  delete(country: Country) {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      width: this.getWidth(),
      data: { item: this.translateService.instant('COUNTRIES.DELETEFRONT') + ' ' + country.name + ' ' + this.translateService.instant('COUNTRIES.DELETEREAR') }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.apiService.deleteCountry(country.countryId).subscribe(() => {
          this.loadData();
          this.dataUpdateService.requestUpdate();
        });
      }
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
