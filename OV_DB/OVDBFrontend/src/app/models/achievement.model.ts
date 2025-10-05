export interface Achievement {
  id: number;
  key: string;
  name: string;
  nameNL: string;
  description: string;
  descriptionNL: string;
  category: string;
  level: string;
  iconName: string;
  iconUrl?: string;
  thresholdValue: number;
  currentProgress: number;
  isUnlocked: boolean;
  unlockedAt?: Date;
  year?: number;
}

export interface AchievementProgress {
  category: string;
  currentValue: number;
  achievements: Achievement[];
}
