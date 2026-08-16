import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatOption } from '@angular/material/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelect } from '@angular/material/select';
import { TranslateModule } from '@ngx-translate/core';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import { Map } from 'src/app/models/map.model';
import { AchievementFamily } from 'src/app/models/achievements.model';

@Component({
  selector: 'app-achievements',
  templateUrl: './achievements.component.html',
  styleUrl: './achievements.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatCard,
    MatCardTitle,
    MatCardContent,
    MatFormField,
    MatLabel,
    MatIcon,
    MatSelect,
    MatOption,
    MatProgressBarModule,
    MatProgressSpinner,
    TranslateModule,
  ],
})
export class AchievementsComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);

  maps = signal<Map[]>([]);
  selectedMap = signal<string | null>(null);
  families = signal<AchievementFamily[]>([]);
  loading = signal(false);

  ngOnInit(): void {
    this.apiService.getMaps().subscribe((maps) => {
      this.maps.set(maps);
      if (maps.length > 0) {
        const defaultMap = maps.find((m) => m.default) ?? maps[0];
        this.changeMap(defaultMap.mapGuid);
      }
    });
  }

  changeMap(mapGuid: string): void {
    this.selectedMap.set(mapGuid);
    this.loading.set(true);
    this.apiService.getAchievements(mapGuid).subscribe({
      next: (achievements) => {
        this.families.set(achievements.families);
        this.loading.set(false);
      },
      error: () => {
        this.families.set([]);
        this.loading.set(false);
      },
    });
  }

  /** Generated families (one per country) carry their own name; fixed ones are translated. */
  displayName(family: AchievementFamily): string | null {
    return family.name ? this.translationService.getNameForItem({ name: family.name, nameNL: family.nameNL ?? '' }) : null;
  }

  /** Tier pips, so the ladder is visible as progress without listing distant thresholds. */
  tierPips(family: AchievementFamily): boolean[] {
    return Array.from({ length: family.totalTiers }, (_, index) => index < family.earnedTiers);
  }

  progressPercentage(family: AchievementFamily): number {
    return Math.round(family.progressToNext * 100);
  }
}
