import { MyProfileDto } from '../client/partner-client';
import { buildLanguagePushCommand } from './language-preference-sync.models';

const profileJson = {
  email: 'cleaner@cleansia.cz',
  firstName: 'Jana',
  lastName: 'Novakova',
  phoneNumber: '+420777123456',
  birthDate: '1990-05-15',
  preferredLanguageCode: 'en',
  preferredLanguageName: 'English',
};

const profile = (overrides: Record<string, unknown> = {}): MyProfileDto =>
  MyProfileDto.fromJS({ ...profileJson, ...overrides });

describe('buildLanguagePushCommand', () => {
  it('runs west of UTC, where a replayed date-only value can lose a day', () => {
    expect(new Date('1990-05-15').getTimezoneOffset()).toBeGreaterThan(0);
  });

  it('replays the whole profile alongside the new language', () => {
    const command = buildLanguagePushCommand(profile(), 'cs');

    expect(command?.toJSON()).toEqual({
      id: undefined,
      firstName: 'Jana',
      lastName: 'Novakova',
      phoneNumber: '+420777123456',
      birthDate: '1990-05-15',
      photo: undefined,
      languageCode: 'cs',
      removePhoto: false,
    });
  });

  it('carries exactly the members the command declares, so a regen that adds one is caught', () => {
    const command = buildLanguagePushCommand(profile(), 'cs');

    expect(Object.keys(command?.toJSON() ?? {}).sort()).toEqual([
      'birthDate',
      'firstName',
      'id',
      'languageCode',
      'lastName',
      'phoneNumber',
      'photo',
      'removePhoto',
    ]);
  });

  it('keeps the stored birth date on the day the server sent', () => {
    const command = buildLanguagePushCommand(profile(), 'cs');

    expect(command?.toJSON()['birthDate']).toBe('1990-05-15');
  });

  it('sends a missing phone number as a blank string on the wire, never an absent member', () => {
    const command = buildLanguagePushCommand(profile({ phoneNumber: undefined }), 'cs');
    const body = JSON.parse(JSON.stringify(command?.toJSON()));

    expect(body).toHaveProperty('phoneNumber');
    expect(body.phoneNumber).toBe('');
  });

  it('pushes for a cleaner who has no phone number yet — the precondition is the name', () => {
    expect(buildLanguagePushCommand(profile({ phoneNumber: '' }), 'cs')).not.toBeNull();
    expect(buildLanguagePushCommand(profile({ birthDate: undefined }), 'cs')).not.toBeNull();
  });

  it('sends the tag that was picked, not the one already stored', () => {
    expect(buildLanguagePushCommand(profile({ preferredLanguageCode: 'ru' }), 'uk')?.languageCode).toBe('uk');
  });

  it('refuses a profile whose names the server would reject, rather than blanking them', () => {
    expect(buildLanguagePushCommand(profile({ firstName: '' }), 'cs')).toBeNull();
    expect(buildLanguagePushCommand(profile({ lastName: '   ' }), 'cs')).toBeNull();
    expect(buildLanguagePushCommand(profile({ firstName: undefined }), 'cs')).toBeNull();
    expect(buildLanguagePushCommand(undefined, 'cs')).toBeNull();
  });

  it('writes nothing when the server already holds the picked language', () => {
    expect(buildLanguagePushCommand(profile({ preferredLanguageCode: 'cs' }), 'cs')).toBeNull();
  });
});
