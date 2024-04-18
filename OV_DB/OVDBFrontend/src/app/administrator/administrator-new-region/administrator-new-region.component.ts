import { Component, Inject } from "@angular/core";
import { MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { NewRegion, Region } from "src/app/models/region.model";

@Component({
  selector: "app-administrator-new-region",
  templateUrl: "./administrator-new-region.component.html",
  styleUrl: "./administrator-new-region.component.scss",
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
