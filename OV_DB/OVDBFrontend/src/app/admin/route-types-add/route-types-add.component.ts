import { Component, OnInit, Inject } from "@angular/core";
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from "@angular/material/dialog";
import { ApiService } from "src/app/services/api.service";
import { RouteType } from "src/app/models/routeType.model";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { CdkScrollable } from "@angular/cdk/scrolling";
import { MatFormField, MatLabel } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { FormsModule } from "@angular/forms";
import { MatCheckbox } from "@angular/material/checkbox";
import { MatButton } from "@angular/material/button";

@Component({
    selector: "app-route-types-add",
    templateUrl: "./route-types-add.component.html",
    styleUrls: ["./route-types-add.component.scss"],
    imports: [
        MatDialogTitle,
        CdkScrollable,
        MatDialogContent,
        MatFormField,
        MatLabel,
        MatInput,
        FormsModule,
        MatCheckbox,
        MatDialogActions,
        MatButton,
        TranslateModule,
    ]
})
export class RouteTypesAddComponent implements OnInit {
  routeTypeName: string;
  routeTypeNameNL: string;
  loading: boolean;
  error: string;
  colour: string;
  id: number;
  isTrain = false;
  constructor(
    public dialogRef: MatDialogRef<RouteTypesAddComponent>,
    private translateService: TranslateService,
    private apiService: ApiService,
    @Inject(MAT_DIALOG_DATA) data
  ) {
    if (!!data && data.routeType) {
      this.id = data.routeType.typeId;
      this.colour = data.routeType.colour;
      this.routeTypeName = data.routeType.name;
      this.routeTypeNameNL = data.routeType.nameNL;
      this.isTrain = data.routeType.isTrain;
    }
  }

  ngOnInit() {}

  cancel() {
    this.dialogRef.close();
  }

  return() {
    if (!this.routeTypeName) {
      return;
    }
    this.loading = true;
    if (!this.id) {
      this.apiService
        .addRouteType({
          name: this.routeTypeName,
          colour: this.colour,
          nameNL: this.routeTypeNameNL,
          isTrain: this.isTrain,
        } as RouteType)
        .subscribe(
          () => {
            this.dialogRef.close(true);
          },
          (err) => {
            this.error =
              this.translateService.instant("ADDTYPE.ERROR") + " " + err.error;
          }
        );
    } else {
      this.apiService
        .updateRouteType({
          typeId: this.id,
          name: this.routeTypeName,
          colour: this.colour,
          nameNL: this.routeTypeNameNL,
          isTrain: this.isTrain,
        } as RouteType)
        .subscribe(
          () => {
            this.dialogRef.close(true);
          },
          (err) => {
            this.error =
              this.translateService.instant("ADDTYPE.ERROR") + " " + err.error;
          }
        );
    }
  }
}
