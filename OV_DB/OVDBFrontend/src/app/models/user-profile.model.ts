export interface TraewellingTagMapping {
  fromTag: string;
  toTag: string;
}

export interface UserProfile {
  email: string;
  preferredLanguage: string | null;
  telegramUserId: number | null;
  hasTraewelling: boolean | null;
  trainlogMaterialKey: string;
  trainlogRegistrationKey: string;
  trainlogSeatKey: string;
  enableTrainlogExport: boolean;
  traewellingTagMappings: TraewellingTagMapping[];
}

export interface UpdateProfile {
  preferredLanguage: string | null;
  telegramUserId: number | null;
  trainlogMaterialKey: string;
  trainlogRegistrationKey: string;
  trainlogSeatKey: string;
  traewellingTagMappings: TraewellingTagMapping[];
}

export interface ChangePassword {
  currentPassword: string;
  newPassword: string;
}