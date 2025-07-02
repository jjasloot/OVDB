import { Component, inject, Input, OnInit, signal } from '@angular/core';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RegionStat } from '../../models/region.model';
import { ApiService } from '../../services/api.service';
import { NgFor } from '@angular/common';
import { TranslationService } from '../../services/translation.service';
import { RegionStatsDisplayComponent } from "./region-stats-display/region-stats-display.component";

@Component({
  selector: 'app-region-stat',
  templateUrl: './region-stat.component.html',
  styleUrls: ['./region-stat.component.scss'],
  standalone: true,
  imports: [MatListModule, MatIconModule, MatProgressBarModule, RegionStatsDisplayComponent]
})
export class RegionStatComponent implements OnInit {
  regionStats = signal([] as RegionStat[]);

  private readonly apiService = inject(ApiService);
 
  ngOnInit(): void {
    this.apiService.getRegionStats().subscribe(stats => {
      this.regionStats.set(stats);
    });
  }

 
}
