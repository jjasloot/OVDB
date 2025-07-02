import { Component, OnInit, computed, inject, signal } from "@angular/core";
import { ApiService } from "src/app/services/api.service";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatListModule } from "@angular/material/list";
import { MatDividerModule } from "@angular/material/divider";
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FormsModule } from '@angular/forms';
import { AsyncPipe, NgClass } from "@angular/common";
import { OperatorService } from "../services/operator.service";
import { RegionOperator, RegionOperators } from "../models/region-operators.model";
import { TranslationService } from "../services/translation.service";
import { toSignal } from "@angular/core/rxjs-interop";

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
    MatButtonToggleModule,
    MatSlideToggleModule,
    FormsModule,
    AsyncPipe,
    NgClass
  ],
})
export class UsedOperatorsComponent implements OnInit {
  apiService = inject(ApiService);
  operatorService = inject(OperatorService)
  translationService = inject(TranslationService);
  regions = signal<RegionOperators[]>([]);
  compact = false;
  currentLanguage = toSignal(this.translationService.languageChanged);

  sortedRegion = computed(() => {
    this.currentLanguage(); //so we update when the language changes
    return this.regions().sort((a, b) => {
      if (this.name(a) < this.name(b)) {
        return -1;
      }
      if (this.name(a) > this.name(b)) {
        return 1;
      }
      return 0;
    })
  });

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

  toggleCompact() {
    this.compact = !this.compact;
  }
}
