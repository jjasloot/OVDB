import { Component, OnInit, signal } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { TimeStatsComponent } from "./time-stats/time-stats.component";
import { TranslateModule } from '@ngx-translate/core';
import { UsedOperatorsComponent } from "../used-operators/used-operators.component";
@Component({
  selector: 'app-stats',
  templateUrl: './stats.component.html',
  styleUrls: ['./stats.component.scss'],
  imports: [MatTabsModule, TimeStatsComponent, TranslateModule, UsedOperatorsComponent]
})
export class StatsComponent {
}
