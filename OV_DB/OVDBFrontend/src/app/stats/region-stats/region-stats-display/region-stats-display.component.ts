import { NgClass } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslateModule } from '@ngx-translate/core';
import { RegionStat } from 'src/app/models/region.model';
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-region-stats-display',
  imports: [MatListModule, MatIconModule, MatProgressBarModule, MatCardModule, NgClass, TranslateModule],
  templateUrl: './region-stats-display.component.html',
  styleUrl: './region-stats-display.component.scss'
})
export class RegionStatsDisplayComponent {
  regionStats = input.required<RegionStat[]>();
  private readonly translationService = inject(TranslationService);
  expanded: { [id: number]: boolean } = {};

  name(region: RegionStat) {
    return this.translationService.getNameForItem(region);
  }

  toggle(regionId: number) {
    this.expanded[regionId] = !this.expanded[regionId];
  }

  isExpanded(regionId: number): boolean {
    return !!this.expanded[regionId];
  }

  currentLanguage = toSignal(this.translationService.languageChanged);

  sortedRegionStats = computed(() => {
    this.currentLanguage(); //so we update when the language changes
    return this.regionStats().sort((a, b) => {
      if (this.name(a) < this.name(b)) {
        return -1;
      }
      if (this.name(a) > this.name(b)) {
        return 1;
      }
      return 0;
    })
  });

  visitedCount(regions: RegionStat[]) {
    return regions.filter(r => r.visited).length;
  }

  getRegionClass(region: RegionStat): string {
    const totalChildren = region.children?.length ?? 0;
    const visitedChildren = this.visitedCount(region.children ?? []);
    if (!region.visited && totalChildren === 0) {
      return 'region-unvisited';
    } else if (totalChildren > 0) {
      if (visitedChildren === 0) {
        return 'region-unvisited';
      } else if (visitedChildren === totalChildren) {
        return 'region-visited';
      } else {
        return 'region-partial';
      }
    } else if (region.visited) {
      return 'region-visited';
    }
    return '';
  }
}
