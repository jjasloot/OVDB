import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatChipsModule } from '@angular/material/chips';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ApiService } from '../services/api.service';
import { Achievement, AchievementProgress } from '../models/achievement.model';

@Component({
  selector: 'app-achievements',
  templateUrl: './achievements.component.html',
  styleUrls: ['./achievements.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatProgressBarModule,
    MatIconModule,
    MatTabsModule,
    MatChipsModule,
    TranslateModule
  ]
})
export class AchievementsComponent implements OnInit {
  achievementCategories = signal<AchievementProgress[]>([]);
  loading = signal(true);

  constructor(
    private apiService: ApiService,
    private translate: TranslateService
  ) {}

  ngOnInit(): void {
    this.loadAchievements();
  }

  loadAchievements(): void {
    this.loading.set(true);
    this.apiService.getAchievements().subscribe({
      next: (data) => {
        this.achievementCategories.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading achievements:', err);
        this.loading.set(false);
      }
    });
  }

  getCategoryName(category: string): string {
    const categoryNames: { [key: string]: string } = {
      'distance_overall': 'ACHIEVEMENTS.CATEGORIES.DISTANCE',
      'distance_yearly': 'ACHIEVEMENTS.CATEGORIES.DISTANCE_YEARLY',
      'stations': 'ACHIEVEMENTS.CATEGORIES.STATIONS',
      'countries': 'ACHIEVEMENTS.CATEGORIES.COUNTRIES',
      'transport_types': 'ACHIEVEMENTS.CATEGORIES.TRANSPORT_TYPES'
    };
    return categoryNames[category] || category;
  }

  getLevelColor(level: string): string {
    const colors: { [key: string]: string } = {
      'bronze': '#CD7F32',
      'silver': '#C0C0C0',
      'gold': '#FFD700',
      'platinum': '#E5E4E2',
      'diamond': '#B9F2FF'
    };
    return colors[level] || '#888';
  }

  getProgressPercentage(achievement: Achievement): number {
    if (achievement.thresholdValue === 0) return 0;
    return Math.min(100, (achievement.currentProgress / achievement.thresholdValue) * 100);
  }

  getAchievementName(achievement: Achievement): string {
    const currentLang = this.translate.currentLang || this.translate.defaultLang;
    return currentLang === 'nl' && achievement.nameNL ? achievement.nameNL : achievement.name;
  }

  getAchievementDescription(achievement: Achievement): string {
    const currentLang = this.translate.currentLang || this.translate.defaultLang;
    return currentLang === 'nl' && achievement.descriptionNL ? achievement.descriptionNL : achievement.description;
  }
}
