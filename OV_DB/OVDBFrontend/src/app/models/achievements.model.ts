export interface AchievementTier {
  tier: number;
  threshold: number;
  earned: boolean;
  earnedOn: string | null;
}

export interface AchievementFamily {
  key: string;
  icon: string;
  /** Formatting hint: "km", "kmh", "hours" or "count". */
  unit: string;
  currentValue: number;
  earnedTiers: number;
  totalTiers: number;
  currentTier: AchievementTier | null;
  nextTier: AchievementTier | null;
  progressToNext: number;
}

export interface Achievements {
  families: AchievementFamily[];
}
