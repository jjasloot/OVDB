import { formatNumber } from '@angular/common';
import { TranslateService } from '@ngx-translate/core';
import { RouterLink } from '@angular/router';
import { Component, OnInit, computed, inject, signal, ChangeDetectionStrategy } from "@angular/core";
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
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { TranslateModule } from "@ngx-translate/core";

@Component({
  selector: "app-used-operators",
  templateUrl: "./used-operators.component.html",
  styleUrls: ["./used-operators.component.scss"],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    MatCardModule,
    MatIconModule,
    MatListModule,
    MatDividerModule,
    MatButtonToggleModule,
    MatSlideToggleModule,
    FormsModule,
    AsyncPipe,
    NgClass,
    MatProgressSpinnerModule,
    TranslateModule,
    RouterLink
  ],
})
export class UsedOperatorsComponent implements OnInit {
  apiService = inject(ApiService);
  operatorService = inject(OperatorService)
  translationService = inject(TranslationService);
  private translateService = inject(TranslateService);
  regions = signal<RegionOperators[]>([]);
  compact = false;
  currentLanguage = toSignal(this.translationService.languageChanged);
  loading = signal(true);

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
    this.apiService.getOperatorsGroupedByRegion().subscribe({
      next: (data) => {
        this.regions.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** Name, plus your trips and km with this operator when there are any. */
  tileTitle(operator: RegionOperator): string {
    const name = operator.operatorNames[0];
    if (!operator.trips) {
      return name;
    }
    return name + " · " + this.translateService.instant("STATS.OPERATOR_TOTALS", {
      trips: formatNumber(operator.trips, this.translationService.dateLocale),
      km: formatNumber(operator.distanceKm, this.translationService.dateLocale, "1.0-0"),
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
