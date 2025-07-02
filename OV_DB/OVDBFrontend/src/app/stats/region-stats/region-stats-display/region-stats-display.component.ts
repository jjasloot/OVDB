import { Component, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RegionStat } from 'src/app/models/region.model';
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-region-stats-display',
  imports: [MatListModule, MatIconModule, MatProgressBarModule],
  templateUrl: './region-stats-display.component.html',
  styleUrl: './region-stats-display.component.scss'
})
export class RegionStatsDisplayComponent {
  regionStats = input.required<RegionStat[]>();
  private readonly translationService = inject(TranslationService);
  name(region: RegionStat) {
    return this.translationService.getNameForItem(region);
  }
}
