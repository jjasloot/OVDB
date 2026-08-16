import { Component, ChangeDetectionStrategy, OnInit, inject, signal } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { TimeStatsComponent } from "./time-stats/time-stats.component";
import { TranslateModule } from '@ngx-translate/core';
import { UsedOperatorsComponent } from "../used-operators/used-operators.component";
import { RegionStatComponent } from './region-stats/region-stat.component';
import { PunctualityStatsComponent } from './punctuality-stats/punctuality-stats.component';
import { YearInReviewComponent } from './year-in-review/year-in-review.component';
import { StationCompletionComponent } from './station-completion/station-completion.component';
import { MapReplayComponent } from './map-replay/map-replay.component';
import { AchievementsComponent } from './achievements/achievements.component';
import { ApiService } from '../services/api.service';
import { provideCharts, withDefaultRegisterables } from "ng2-charts";
// Only the (lazily-loaded) stats screen uses charts, so pull chart.js and the zoom
// plugin into this chunk instead of the eager main bundle.
import "chartjs-plugin-zoom";

@Component({
  selector: 'app-stats',
  templateUrl: './stats.component.html',
  styleUrls: ['./stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTabsModule, TimeStatsComponent, TranslateModule, UsedOperatorsComponent, RegionStatComponent, PunctualityStatsComponent, YearInReviewComponent, StationCompletionComponent, MapReplayComponent, AchievementsComponent],
  providers: [provideCharts(withDefaultRegisterables())]
})
export class StatsComponent implements OnInit {
  private apiService = inject(ApiService);

  /** Optional tabs, resolved server-side; the endpoints enforce the same rules independently. */
  showAchievements = signal(false);

  ngOnInit(): void {
    this.apiService.getFeatures().subscribe({
      next: (features) => this.showAchievements.set(features.achievements),
      error: () => this.showAchievements.set(false),
    });
  }
}
