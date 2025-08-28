import { Component, computed, model, signal, inject } from "@angular/core";
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
  ]
})
export class AdministratorNewRegionComponent {
  dialogRef = inject<MatDialogRef<AdministratorNewRegionComponent>>(MatDialogRef);

  region = {} as NewRegion;
  regions: Region[] = [];
  parentRegionId = model<number | null>(null);
  intermediateRegionId = model<number | null>(null);
  showAllRegionsForIntermediate = signal(false);
  constructor() {
    const data = inject(MAT_DIALOG_DATA);

    if (!!data && data.regions) {
      this.regions = data.regions;
    }
  }

  cancel() {
    this.dialogRef.close();
  }

  save() {
    this.region.parentRegionId = this.intermediateRegionId() ?? this.parentRegionId();
    this.dialogRef.close(this.region);
  }

  intermediateRegions = computed(() => {
    if (!this.parentRegionId()) {
      return [];
    }
    console.log(this.parentRegionId(), this.regions, this.regions.find(r => r.id == this.parentRegionId())?.subRegions);
    return this.regions.find(r => r.id == this.parentRegionId()).subRegions.filter(sr => sr.subRegions.length > 0 || this.showAllRegionsForIntermediate());
  });

  showAllRegions() {
    this.showAllRegionsForIntermediate.set(true);
  }

}
