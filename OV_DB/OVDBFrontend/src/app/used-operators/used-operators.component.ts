import { Component, OnInit, inject, signal } from "@angular/core";
import { ApiService } from "../services/api.service";
import { RegionOperatorsDTO } from "../models/region-operators.model";
import { MatCard } from "@angular/material/card";
import { MatList, MatListItem } from "@angular/material/list";
import { MatIcon } from "@angular/material/icon";
import { AsyncPipe, NgFor, NgIf } from "@angular/common";

@Component({
  selector: "app-used-operators",
  templateUrl: "./used-operators.component.html",
  styleUrls: ["./used-operators.component.scss"],
  imports: [MatCard, MatList, MatListItem, MatIcon, AsyncPipe, NgFor, NgIf],
})
export class UsedOperatorsComponent implements OnInit {
  apiService = inject(ApiService);
  regionsWithOperators = signal<RegionOperatorsDTO[]>([]);

  ngOnInit(): void {
    this.apiService.getOperatorsGroupedByRegion().subscribe((data) => {
      this.regionsWithOperators.set(data);
    });
  }

  getLogo(operatorId: number): string {
    return this.apiService.getOperatorLogo(operatorId);
  }
}
