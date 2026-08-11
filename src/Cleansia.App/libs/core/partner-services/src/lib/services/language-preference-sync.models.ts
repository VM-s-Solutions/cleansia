import { MyProfileDto, UpdateCurrentUserCommand } from '../client/partner-client';

/**
 * The generated serializer emits a date-only member from the value's LOCAL calendar components,
 * while the generated DTO parses the same member as UTC midnight. West of UTC those disagree, so
 * replaying the parsed value verbatim moves the cleaner's birth date back a day on every push.
 * Re-seating it on local midnight makes both readings name the day the server sent.
 */
export function toWireDateOnly(date: Date | undefined): Date | undefined {
  if (!date) return undefined;
  return new Date(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
}

/**
 * `UpdateCurrentUser` replaces first and last name outright — phone, birth date, photo and language
 * treat an absent value as "nothing to say" — so a language push has to carry the rest of the
 * profile, and must not run on a profile whose names the validators would reject.
 *
 * A missing phone or birth date is not such a hole: a cleaner still completing registration has
 * neither, and both survive the save untouched. The phone still travels, as a blank, because
 * `Command.PhoneNumber` is a non-nullable reference on a `#nullable enable` host and the binder
 * refuses an absent member before the handler runs.
 */
export function buildLanguagePushCommand(
  profile: MyProfileDto | undefined,
  languageCode: string
): UpdateCurrentUserCommand | null {
  if (!profile) return null;
  if (!profile.firstName?.trim() || !profile.lastName?.trim()) return null;
  if (profile.preferredLanguageCode === languageCode) return null;

  const command = new UpdateCurrentUserCommand();
  command.firstName = profile.firstName;
  command.lastName = profile.lastName;
  command.phoneNumber = profile.phoneNumber ?? '';
  command.birthDate = toWireDateOnly(profile.birthDate);
  command.languageCode = languageCode;
  command.removePhoto = false;
  return command;
}
