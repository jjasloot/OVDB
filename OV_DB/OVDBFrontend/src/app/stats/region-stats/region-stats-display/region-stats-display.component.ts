import { NgClass } from '@angular/common';
import { Component, computed, inject, input, ChangeDetectionStrategy } from '@angular/core';
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
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './region-stats-display.component.scss'
})
export class RegionStatsDisplayComponent {
  regionStats = input.required<RegionStat[]>();
  private readonly translationService = inject(TranslationService);
  expanded: Record<number, boolean> = {};

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

  sortBy = input<'name' | 'completion'>('name');
  hideUnvisited = input(false);

  /** Share of the region done: its stations if it has any, otherwise its sub-regions. */
  completion(region: RegionStat): number {
    if (region.totalStations > 0) {
      return region.visitedStations / region.totalStations;
    }
    const children = region.children?.length ?? 0;
    return children > 0 ? this.visitedCount(region.children) / children : region.visited ? 1 : 0;
  }

  private touched(region: RegionStat): boolean {
    return region.visited || region.visitedStations > 0 || this.visitedCount(region.children ?? []) > 0;
  }

  sortedRegionStats = computed(() => {
    this.currentLanguage(); //so we update when the language changes
    const byName = (a: RegionStat, b: RegionStat) => this.name(a).localeCompare(this.name(b));
    const list = this.regionStats().filter((r) => !this.hideUnvisited() || this.touched(r));
    return [...list].sort(this.sortBy() === 'completion'
      ? (a, b) => this.completion(b) - this.completion(a) || byName(a, b)
      : byName);
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
