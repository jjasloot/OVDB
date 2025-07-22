export interface UserProfile {
  email: string;
  preferredLanguage: string;
  telegramUserId: number | null;
}

export interface UpdateProfile {
  preferredLanguage: string;
  telegramUserId: number | null;
}

export interface ChangePassword {
  currentPassword: string;
  newPassword: string;
}