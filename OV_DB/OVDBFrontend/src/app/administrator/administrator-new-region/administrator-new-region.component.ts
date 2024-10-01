import { Component, Inject } from "@angular/core";
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogTitle, MatDialogContent, MatDialogActions } from "@angular/material/dialog";
import { NewRegion, Region } from "src/app/models/region.model";
import { CdkScrollable } from "@angular/cdk/scrolling";
import { MatFormField, MatLabel } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { FormsModule } from "@angular/forms";
import { MatSelect } from "@angular/material/select";
import { MatOption } from "@angular/material/core";
import { MatButton } from "@angular/material/button";
import { TranslateModule } from "@ngx-translate/core";

@Component({
    selector: "app-administrator-new-region",
    templateUrl: "./administrator-new-region.component.html",
    styleUrl: "./administrator-new-region.component.scss",
    standalone: true,
    imports: [
        MatDialogTitle,
        CdkScrollable,
        MatDialogContent,
        MatFormField,
        MatLabel,
        MatInput,
        FormsModule,
        MatSelect,
        MatOption,
        MatDialogActions,
        MatButton,
        TranslateModule,
    ],
})
export class AdministratorNewRegionComponent {
  region = {} as NewRegion;
  regions: Region[] = [];

  constructor(
    public dialogRef: MatDialogRef<AdministratorNewRegionComponent>,
    @Inject(MAT_DIALOG_DATA) data
  ) {
    if (!!data && data.regions) {
      this.regions = data.regions;
    }
  }

  cancel() {
    this.dialogRef.close();
  }

  save() {
    this.dialogRef.close(this.region);
  }
}
