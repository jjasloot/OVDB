import { Component, OnInit, Inject } from '@angular/core';
import { MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ApiService } from 'src/app/services/api.service';
import { Country } from 'src/app/models/country.model';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-country-add',
  templateUrl: './country-add.component.html',
  styleUrls: ['./country-add.component.scss']
})
export class CountryAddComponent implements OnInit {
  countryName: string;
  countryNameNL: string;

  id: number;
  loading: boolean;
  error: string;
  constructor(
    public dialogRef: MatDialogRef<CountryAddComponent>,
    private translateService: TranslateService,
    private apiService: ApiService,
    @Inject(MAT_DIALOG_DATA) data) {
    if (!!data && data.country) {
      this.countryName = data.country.name;
      this.countryNameNL = data.country.nameNL;
      this.id = data.country.countryId;
    }
  }

  ngOnInit() {

  }

  cancel() {
    this.dialogRef.close();
  }

  return() {
    if (!this.countryName) {
      return;
    }
    this.loading = true;
    if (!this.id) {
      this.apiService.addCountry({ name: this.countryName, nameNL: this.countryNameNL } as Country).subscribe(() => {
        this.dialogRef.close(true);
      }, err => {
        this.error = this.translateService.instant('ADDTYPE.ERROR') + ' ' + err.error;
      });
    } else {
      this.apiService.updateCountry({ countryId: this.id, name: this.countryName, nameNL: this.countryNameNL } as Country).subscribe(() => {
        this.dialogRef.close(true);
      }, err => {
        this.error = this.translateService.instant('ADDTYPE.ERROR') + ' ' + err.error;
      });
    }

  }


}
