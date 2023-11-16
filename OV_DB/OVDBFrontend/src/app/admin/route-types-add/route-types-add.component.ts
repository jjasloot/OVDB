import { Component, OnInit, Inject } from '@angular/core';
import { MatLegacyDialogRef as MatDialogRef, MAT_LEGACY_DIALOG_DATA as MAT_DIALOG_DATA } from '@angular/material/legacy-dialog';
import { ApiService } from 'src/app/services/api.service';
import { RouteType } from 'src/app/models/routeType.model';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-route-types-add',
  templateUrl: './route-types-add.component.html',
  styleUrls: ['./route-types-add.component.scss']
})
export class RouteTypesAddComponent implements OnInit {
  routeTypeName: string;
  routeTypeNameNL: string;
  loading: boolean;
  error: string;
  colour: string;
  id: number;
  constructor(
    public dialogRef: MatDialogRef<RouteTypesAddComponent>,
    private translateService: TranslateService,
    private apiService: ApiService,
    @Inject(MAT_DIALOG_DATA) data) {
    if (!!data && data.routeType) {
      this.id = data.routeType.typeId;
      this.colour = data.routeType.colour;
      this.routeTypeName = data.routeType.name;
      this.routeTypeNameNL = data.routeType.nameNL;
    }
  }

  ngOnInit() {
  }

  cancel() {
    this.dialogRef.close();
  }

  return() {
    if (!this.routeTypeName) {
      return;
    }
    this.loading = true;
    if (!this.id) {
      this.apiService.addRouteType({
        name: this.routeTypeName,
        colour: this.colour,
        nameNL: this.routeTypeNameNL
      } as RouteType).subscribe(() => {
        this.dialogRef.close(true);
      }, err => {
        this.error = this.translateService.instant('ADDTYPE.ERROR') + ' ' + err.error;
      });
    } else {
      this.apiService.updateRouteType({
        typeId: this.id,
        name: this.routeTypeName,
        colour: this.colour,
        nameNL: this.routeTypeNameNL
      } as RouteType).subscribe(() => {
        this.dialogRef.close(true);
      }, err => {
        this.error = this.translateService.instant('ADDTYPE.ERROR') + ' ' + err.error;
      });
    }

  }


}
