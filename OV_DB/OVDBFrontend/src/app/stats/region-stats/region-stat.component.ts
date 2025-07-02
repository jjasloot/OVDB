import { Component, inject, OnInit, signal } from '@angular/core';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RegionStat } from '../../models/region.model';
import { ApiService } from '../../services/api.service';
import { TranslationService } from '../../services/translation.service';
import { RegionStatsDisplayComponent } from "./region-stats-display/region-stats-display.component";
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-region-stat',
  templateUrl: './region-stat.component.html',
  styleUrls: ['./region-stat.component.scss'],
  standalone: true,
  imports: [MatListModule, MatIconModule, MatProgressBarModule, RegionStatsDisplayComponent, MatProgressSpinnerModule]
})
export class RegionStatComponent implements OnInit {
  regionStats = signal([] as RegionStat[]);
  loading = signal(true);
  private readonly apiService = inject(ApiService);
  private readonly translationService = inject(TranslationService);



  ngOnInit(): void {
    this.apiService.getRegionStats().subscribe(stats => {
      this.regionStats.set(stats);
      this.loading.set(false);
    });
  }

  name(region: RegionStat) {
    return this.translationService.getNameForItem(region);
  }

}
