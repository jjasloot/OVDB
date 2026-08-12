import {
  Component,
  effect,
  inject,
  model,
  OnInit,
  signal,
  ChangeDetectionStrategy
} from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
import { Observable } from "rxjs";
import { Operator } from "src/app/models/operator.model";
import { Region } from "src/app/models/region.model";
import { OperatorService } from "src/app/services/operator.service";
import { RegionsService } from "src/app/services/regions.service";
import { AdministratorOperatorEditDialogComponent } from "./administrator-operator-edit-dialog/administrator-operator-edit-dialog.component";
import {
  MatCard,
  MatCardHeader,
  MatCardTitle,
  MatCardSubtitle,
  MatCardContent,
  MatCardActions,
} from "@angular/material/card";
import { MatIconButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import {
  MatTable,
  MatColumnDef,
  MatHeaderCellDef,
  MatHeaderCell,
  MatCellDef,
  MatCell,
  MatHeaderRowDef,
  MatHeaderRow,
  MatRowDef,
  MatRow,
} from "@angular/material/table";
import { MatChipSet, MatChip } from "@angular/material/chips";
import {
  MatExpansionPanel,
  MatExpansionPanelHeader,
  MatExpansionPanelTitle,
} from "@angular/material/expansion";
import { MatSelect } from "@angular/material/select";
import { FormsModule } from "@angular/forms";
import { MatOption } from "@angular/material/core";
import { AsyncPipe } from "@angular/common";
import { EXTRA_WIDE_DIALOG } from "src/app/constants/dialog-sizes";

@Component({
  selector: "app-administrator-operators",
  templateUrl: "./administrator-operators.component.html",
  styleUrl: "./administrator-operators.component.scss",
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    MatCard,
    MatCardHeader,
    MatCardTitle,
    MatCardSubtitle,
    MatIconButton,
    MatIcon,
    MatCardContent,
    MatTable,
    MatColumnDef,
    MatHeaderCellDef,
    MatHeaderCell,
    MatCellDef,
    MatCell,
    MatChipSet,
    MatChip,
    MatHeaderRowDef,
    MatHeaderRow,
    MatRowDef,
    MatRow,
    MatCardActions,
    MatExpansionPanel,
    MatExpansionPanelHeader,
    MatExpansionPanelTitle,
    MatSelect,
    FormsModule,
    MatOption,
    AsyncPipe,
  ]
})
export class AdministratorOperatorsComponent implements OnInit {
  regionsService = inject(RegionsService);
  operatorService = inject(OperatorService);
  dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
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
  selectedRegion = model<number | null>(null);
  regions = signal<Region[]>([]);
  openOperators = signal<string[]>([]);

  ngOnInit(): void {
    this.getData();
  }

  updateOpenOperators = effect(
    () => {
      if (this.selectedRegion()) {
        this.operatorService
          .getOpenOperatorsForRegion(this.selectedRegion()!)
          .subscribe((data) => {
            this.openOperators.set(data);
          });
      }
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
        ...EXTRA_WIDE_DIALOG,
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
      .subscribe({
        next: (count) => {
          this.reconnecting.set(
            this.reconnecting().filter((id) => id !== operator.id)
          );
          this.snackBar.open(operator.names[0] + ": " + count + " routes connected", undefined, { duration: 4000 });
        },
        error: () => {
          this.reconnecting.set(
            this.reconnecting().filter((id) => id !== operator.id)
          );
        },
      });
  }

  newOperator() {
    const dialogRef = this.dialog.open(
      AdministratorOperatorEditDialogComponent,
      {
        ...EXTRA_WIDE_DIALOG,
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
