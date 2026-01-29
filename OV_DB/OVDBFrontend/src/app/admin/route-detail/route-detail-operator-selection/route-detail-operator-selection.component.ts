import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  OnInit,
  Output,
  signal,
} from "@angular/core";
import { FormControl, FormsModule, ReactiveFormsModule } from "@angular/forms";
import { Observable, Subject } from "rxjs";
import { debounceTime } from "rxjs/operators";
import { OperatorMinimal } from "src/app/models/operator.model";
import { OperatorService } from "src/app/services/operator.service";
import { MatFormField, MatLabel, MatHint } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { MatAutocompleteTrigger, MatAutocomplete } from "@angular/material/autocomplete";
import { MatOption } from "@angular/material/core";
import { AsyncPipe } from "@angular/common";
import { TranslateModule } from "@ngx-translate/core";

@Component({
  selector: "app-route-detail-operator-selection",
  templateUrl: "./route-detail-operator-selection.component.html",
  styleUrl: "./route-detail-operator-selection.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatFormField,
    MatLabel,
    MatInput,
    FormsModule,
    MatAutocompleteTrigger,
    ReactiveFormsModule,
    MatHint,
    MatAutocomplete,
    MatOption,
    AsyncPipe,
    TranslateModule,
  ]
})
export class RouteDetailOperatorSelectionComponent implements OnInit {
  formField = input.required<FormControl<string>>();
  routeId = input.required<number>();
  @Output() activeOperators = new Subject<number[]>();
  selectedOperators = signal<number[]>([]);
  operators = signal<OperatorMinimal[]>([]);
  operatorService = inject(OperatorService);
  filteredOperators = signal<OperatorMinimal[]>([]);
  otherSelectedOperators = signal<string>(null);
  updateEffect = effect(
    () => {
      this.activeOperators.next(this.selectedOperators());
    },
    { allowSignalWrites: true }
  );

  getLogo(operatorId: number): Observable<string> {
    return this.operatorService.getOperatorLogo(operatorId);
  }

  ngOnInit(): void {
    this.operatorService.getOperatorNamesForRoute(this.routeId()).subscribe({
      next: (operators) => {
        this.operators.set(operators);
        this.operatorSelected();
      },
    });
    this.formField().valueChanges.subscribe(() => {
      if (this.formField().value == null) {
        this.otherSelectedOperators.set(null);
        return;
      }
      this.otherSelectedOperators.set(
        this.formField().value.substring(
          0,
          this.formField().value.lastIndexOf(";") + 1
        )
      );
    });
    this.formField()
      .valueChanges.pipe(debounceTime(200))
      .subscribe(() => {
        this.operatorSelected();
      });

    this.formField().valueChanges.subscribe(() => {
      if (this.formField().value == null) {
        this.filteredOperators.set(this.operators());
        return;
      }
      const lastOperator = this.formField().value.split(";").pop().trim();
      if (lastOperator.length < 3) {
        this.filteredOperators.set(
          this.operators().filter(
            (o) => o.name.toLowerCase() === lastOperator.toLowerCase()
          )
        );
        return;
      }

      this.filteredOperators.set(
        this.operators().filter((o) =>
          o.name.toLowerCase().includes(lastOperator.toLowerCase())
        )
      );
    });
  }

  operatorSelected() {
    if (!this.formField().value) {
      this.selectedOperators.set([]);
      return;
    }
    const operators = (this.formField().value as string).split(";");
    const operatorIds: number[] = [];
    operators.forEach((o) => {
      const selectedOperator = this.operators().find(
        (op) => op.name.toLowerCase() === o.trim().toLowerCase()
      );
      if (selectedOperator) {
        operatorIds.push(selectedOperator.id);
      }
    });
    this.selectedOperators.set(operatorIds);
  }
}
