import {
  Component,
  effect,
  inject,
  Inject,
  OnInit,
  signal,
} from "@angular/core";
import {
  FormGroup,
  FormControl,
  FormBuilder,
  Validators,
} from "@angular/forms";
import { MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { Operator } from "src/app/models/operator.model";
import { RegionMinimal } from "src/app/models/region.model";
import { RegionsService } from "src/app/services/regions.service";

@Component({
  selector: "app-administrator-operator-edit-dialog",
  templateUrl: "./administrator-operator-edit-dialog.component.html",
  styleUrl: "./administrator-operator-edit-dialog.component.scss",
})
export class AdministratorOperatorEditDialogComponent {
  nameCtrl = new FormControl("");
  regionService = inject(RegionsService);
  regions = signal<RegionMinimal[]>([]); // This should be populated with actual regions

  loadRegions = effect(
    () => {
      this.regionService.getRegions().subscribe((data) => {
        this.regions.set(data);
      });
    },
    { allowSignalWrites: true }
  );
  operatorForm = this.fb.group({
    names: [this.data.names, [Validators.required, Validators.minLength(1)]],
    regionIds: [this.data.regions.map((region) => region.id)],
  });

  constructor(
    public dialogRef: MatDialogRef<AdministratorOperatorEditDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Operator,
    private fb: FormBuilder
  ) {}

  addName(event: any): void {
    const input = event.input;
    const value = event.value;

    if ((value || "").trim()) {
      this.operatorForm.get("names").value.push(value.trim());
      this.operatorForm.get("names").updateValueAndValidity();
    }

    if (input) {
      input.value = "";
    }
  }

  removeName(name: string): void {
    const index = this.operatorForm.get("names").value.indexOf(name);

    if (index >= 0) {
      this.operatorForm.get("names").value.splice(index, 1);
      this.operatorForm.get("names").updateValueAndValidity();
    }
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onSave(): void {
    this.dialogRef.close(this.operatorForm.value);
  }
}
