import { Component, OnInit, inject, signal } from "@angular/core";
import { ApiService } from "src/app/services/api.service";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatListModule } from "@angular/material/list";
import { MatDividerModule } from "@angular/material/divider";
import { AsyncPipe, NgClass } from "@angular/common";
import { OperatorService } from "../services/operator.service";
import { RegionOperator, RegionOperators } from "../models/region-operators.model";
import { TranslationService } from "../services/translation.service";

@Component({
  selector: "app-used-operators",
  templateUrl: "./used-operators.component.html",
  styleUrls: ["./used-operators.component.scss"],
  standalone: true,
  imports: [
    MatCardModule,
    MatIconModule,
    MatListModule,
    MatDividerModule,
    AsyncPipe,
    NgClass
],
})
export class UsedOperatorsComponent implements OnInit {
  apiService = inject(ApiService);
  operatorService = inject(OperatorService)
  translationService = inject(TranslationService);
  regions = signal<RegionOperators[]>([]);

  ngOnInit(): void {
    this.apiService.getOperatorsGroupedByRegion().subscribe((data) => {
      this.regions.set(data);
    });
  }

  getLogo(operator: RegionOperator) {
    return this.operatorService.getOperatorLogo(operator.operatorId)
  }

  name(region: RegionOperators) {
    return this.translationService.getNameForItem(region);
  }
}
