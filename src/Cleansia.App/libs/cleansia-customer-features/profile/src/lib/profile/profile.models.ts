import {
  AddSavedAddressCommand,
  BlobFileDto,
  ChangePasswordCommand,
  UpdateCurrentUserCommand,
  UpdateSavedAddressCommand,
} from '@cleansia/customer-services';

export const AVATAR_MAX_SIZE_BYTES = 10 * 1024 * 1024;

/**
 * The intersection of what the backend stores and what it will ever serve as an image.
 * `ImageFileValidator` also accepts bmp and tiff by magic bytes, but `ServedContentType`
 * will only ever hand those back as `application/octet-stream`, and no desktop browser
 * renders a tiff in an `<img>` — so accepting one produces an upload that appears to
 * succeed and an avatar that never appears. SVG is absent on both sides: it is XML that
 * can carry script and would run with the storage origin.
 */
export const AVATAR_ALLOWED_CONTENT_TYPES = [
  'image/jpeg',
  'image/jpg',
  'image/png',
  'image/webp',
  'image/gif',
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

export interface SavedAddressFields {
  label: string;
  street: string;
  city: string;
  zipCode: string;
  countryId?: string;
  latitude: number;
  longitude: number;
}

export function buildChangePasswordCommand(
  email: string | undefined,
  newPassword: string | undefined
): ChangePasswordCommand {
  const command = new ChangePasswordCommand();
  command.email = email;
  // The signed-in flow has no emailed code; the session identifies the user.
  command.code = '';
  command.newPassword = newPassword;
  return command;
}

export function buildAddSavedAddressCommand(
  fields: SavedAddressFields,
  setAsDefault: boolean
): AddSavedAddressCommand {
  const command = new AddSavedAddressCommand();
  command.label = fields.label;
  command.street = fields.street;
  command.city = fields.city;
  command.zipCode = fields.zipCode;
  command.countryId = fields.countryId;
  command.latitude = fields.latitude;
  command.longitude = fields.longitude;
  command.setAsDefault = setAsDefault;
  return command;
}

export function buildUpdateSavedAddressCommand(
  savedAddressId: string,
  fields: SavedAddressFields
): UpdateSavedAddressCommand {
  const command = new UpdateSavedAddressCommand();
  command.savedAddressId = savedAddressId;
  command.label = fields.label;
  command.street = fields.street;
  command.city = fields.city;
  command.zipCode = fields.zipCode;
  command.countryId = fields.countryId;
  command.latitude = fields.latitude;
  command.longitude = fields.longitude;
  return command;
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
