export type MapProvider = 'leaflet' | 'maplibre';

export interface UserProfile {
  email: string;
  preferredLanguage: string | null;
  telegramUserId: number | null;
  hasTraewelling: boolean | null;
  preferredMapProvider: MapProvider | null;
}

export interface UpdateProfile {
  preferredLanguage: string | null;
  telegramUserId: number | null;
  preferredMapProvider: MapProvider | null;
}

export interface ChangePassword {
  currentPassword: string;
  newPassword: string;
}