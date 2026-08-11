import { BlobFileDto, UpdateCurrentUserCommand } from '@cleansia/customer-services';

export const AVATAR_MAX_SIZE_BYTES = 10 * 1024 * 1024;

// The backend accepts an avatar by magic bytes (Constants.ImageSignatures, checked by
// ImageFileValidator); these are the browser content types that decode to those signatures.
export const AVATAR_ALLOWED_CONTENT_TYPES = [
  'image/jpeg',
  'image/jpg',
  'image/png',
  'image/webp',
  'image/gif',
  'image/bmp',
  'image/tiff',
] as const;

export const AVATAR_ACCEPT_ATTRIBUTE = AVATAR_ALLOWED_CONTENT_TYPES.join(',');

export interface ProfileDetails {
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  birthDate?: Date;
  languageCode?: string;
}

export type AvatarIntent =
  | { kind: 'unchanged' }
  | { kind: 'upload'; photo: BlobFileDto }
  | { kind: 'remove' };

export type AvatarValidationResult =
  | { valid: true }
  | { valid: false; errorKey: string };

export function validateAvatarFile(file: File): AvatarValidationResult {
  if (file.size > AVATAR_MAX_SIZE_BYTES) {
    return { valid: false, errorKey: 'pages.profile.avatar.size_exceeded' };
  }

  const contentType = file.type.toLowerCase();
  if (!AVATAR_ALLOWED_CONTENT_TYPES.some((allowed) => allowed === contentType)) {
    return { valid: false, errorKey: 'pages.profile.avatar.invalid_type' };
  }

  return { valid: true };
}

export function readFileAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

export function buildAvatarBlobFile(file: File, dataUrl: string): BlobFileDto {
  const photo = new BlobFileDto();
  photo.fileName = file.name;
  photo.base64Content = dataUrl;
  photo.contentType = file.type;
  return photo;
}

export function buildUpdateCurrentUserCommand(
  details: ProfileDetails,
  intent: AvatarIntent
): UpdateCurrentUserCommand {
  const command = new UpdateCurrentUserCommand();
  command.firstName = details.firstName;
  command.lastName = details.lastName;
  command.phoneNumber = details.phoneNumber;
  command.birthDate = details.birthDate;
  command.languageCode = details.languageCode;
  command.removePhoto = intent.kind === 'remove';

  if (intent.kind === 'upload') {
    command.photo = intent.photo;
  }

  return command;
}
