import {
  Component,
  effect,
  inject,
  model,
  OnInit,
  signal,
} from "@angular/core";
import { Operator } from "src/app/models/operator.model";
import { Region } from "src/app/models/region.model";
import { OperatorService } from "src/app/services/operator.service";
import { RegionsService } from "src/app/services/regions.service";

@Component({
  selector: "app-administrator-operators",
  templateUrl: "./administrator-operators.component.html",
  styleUrl: "./administrator-operators.component.scss",
})
export class AdministratorOperatorsComponent implements OnInit {
  regionsService = inject(RegionsService);
  operatorService = inject(OperatorService);
  regions = signal<Region[]>([]);
  selectedRegion = model<Region>();
  openOperators = signal<string[]>([]);

  regionEffect = effect(() => {
    if (!!this.selectedRegion()) {
      this.operatorService
        .getOpenOperatorsForRegion(this.selectedRegion().id)
        .subscribe((operators) => {
          this.openOperators.set(operators);
        });
    }
  });

  ngOnInit(): void {
    this.regionsService.getRegions().subscribe((regions) => {
      this.regions.set(regions);
    });
  }
}
