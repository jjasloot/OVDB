export interface UserProfile {
  email: string;
  preferredLanguage: string;
  telegramUserId: number;
}

export interface UpdateProfile {
  preferredLanguage: string;
  telegramUserId: number;
}

export interface ChangePassword {
  currentPassword: string;
  newPassword: string;
}