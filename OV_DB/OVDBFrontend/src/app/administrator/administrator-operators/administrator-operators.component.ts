import {
  Component,
  effect,
  inject,
  model,
  OnInit,
  signal,
} from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { Observable } from "rxjs";
import { Operator } from "src/app/models/operator.model";
import { Region } from "src/app/models/region.model";
import { OperatorService } from "src/app/services/operator.service";
import { RegionsService } from "src/app/services/regions.service";
import { AdministratorOperatorEditDialogComponent } from "./administrator-operator-edit-dialog/administrator-operator-edit-dialog.component";

@Component({
  selector: "app-administrator-operators",
  templateUrl: "./administrator-operators.component.html",
  styleUrl: "./administrator-operators.component.scss",
})
export class AdministratorOperatorsComponent implements OnInit {
  regionsService = inject(RegionsService);
  operatorService = inject(OperatorService);
  dialog = inject(MatDialog);
  operators = signal<Operator[]>([]);
  displayedColumns: string[] = [
    "id",
    "name",
    "logo",
    "runsTrainsInRegions",
    "restrictToRegions",
    "controls",
  ];
  reconnecting = signal<number[]>([]);
  updating = signal<number[]>([]);
  selectedRegion = model<number>(null);
  regions = signal<Region[]>([]);
  openOperators = signal<string[]>([]);

  ngOnInit(): void {
    this.getData();
  }

  updateOpenOperators = effect(
    () => {
      this.operatorService
        .getOpenOperatorsForRegion(this.selectedRegion())
        .subscribe((data) => {
          this.openOperators.set(data);
        });
    },
    { allowSignalWrites: true }
  );

  private getData() {
    this.operatorService.getOperators().subscribe((data) => {
      this.operators.set(data);
    });
    this.regionsService.getRegions().subscribe((data) => {
      this.regions.set(data);
    });
  }

  getLogo(operatorId: number): Observable<string> {
    return this.operatorService.getOperatorLogo(operatorId);
  }

  editOperator(operator: Operator): void {
    const dialogRef = this.dialog.open(
      AdministratorOperatorEditDialogComponent,
      {
        width: "max(80%, 1200px)",
        data: operator,
      }
    );

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.updating.set([...this.updating(), operator.id]);
        this.operatorService
          .updateOperator(operator.id, result)
          .subscribe(() => {
            this.updating.set(
              this.updating().filter((id) => id !== operator.id)
            );
            this.getData();
          });
      }
    });
  }

  connectOperator(operator: Operator): void {
    this.reconnecting.set([...this.reconnecting(), operator.id]);
    this.operatorService
      .connectRoutesToOperator(operator.id)
      .subscribe((count) => {
        this.reconnecting.set(
          this.reconnecting().filter((id) => id !== operator.id)
        );
        alert(count);
      });
  }

  newOperator() {
    const dialogRef = this.dialog.open(
      AdministratorOperatorEditDialogComponent,
      {
        width: "max(80%, 1200px)",
        data: {
          id: 0,
          names: [],
          runsTrainsInRegions: [],
          restrictToRegions: [],
          logoFilePath: null,
        },
      }
    );

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.operatorService.addOperator(result).subscribe(() => {
          this.getData();
        });
      }
    });
  }

  onLogoClick(operatorId: number) {
    const fileInput = document.getElementById(
      "logo-" + operatorId
    ) as HTMLElement;
    fileInput.click();
  }

  onFileSelected(event: Event, operatorId: number) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      // Handle the file upload logic here
      this.operatorService
        .uploadOperatorLogo(operatorId, file)
        .subscribe(() => {
          this.getData();
        });
    }
  }
}
