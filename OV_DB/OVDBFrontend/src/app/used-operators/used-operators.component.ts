import { Component, OnInit, inject, signal } from "@angular/core";
import { ApiService } from "src/app/services/api.service";
import { RegionOperatorsDTO } from "src/app/models/region-operators.model";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatListModule } from "@angular/material/list";
import { MatDividerModule } from "@angular/material/divider";
import { AsyncPipe, NgFor, NgIf } from "@angular/common";

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
    NgFor,
    NgIf,
  ],
})
export class UsedOperatorsComponent implements OnInit {
  apiService = inject(ApiService);
  regions = signal<RegionOperatorsDTO[]>([]);

  ngOnInit(): void {
    this.apiService.getOperatorsGroupedByRegion().subscribe((data) => {
      this.regions.set(data);
    });
  }

  getLogo(operator: RegionOperatorDTO): string {
    return operator.logoFilePath
      ? operator.logoFilePath
      : "assets/greyed-out-logo.png";
  }
}
