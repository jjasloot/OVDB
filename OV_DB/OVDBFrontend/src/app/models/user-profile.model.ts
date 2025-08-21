export interface UserProfile {
  email: string;
  preferredLanguage: string | null;
  telegramUserId: number | null;
  hasTraewelling: boolean | null;
}

export interface UpdateProfile {
  preferredLanguage: string | null;
  telegramUserId: number | null;
}

export interface ChangePassword {
  currentPassword: string;
  newPassword: string;
}