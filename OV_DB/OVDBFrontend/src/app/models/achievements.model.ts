export interface AchievementTier {
  tier: number;
  threshold: number;
  earned: boolean;
  earnedOn: string | null;
}

export interface AchievementFamily {
  key: string;
  icon: string;
  /** Formatting hint: "km", "kmh", "hours", "minutes" or "count". */
  unit: string;
  /** Set for families generated from data (one per country); null for fixed ones. */
  name: string | null;
  nameNL: string | null;
  descriptionKey: string | null;
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
