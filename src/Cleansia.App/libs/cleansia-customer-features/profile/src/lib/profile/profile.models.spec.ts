import { BlobFileDto } from '@cleansia/customer-services';
import {
  AVATAR_ALLOWED_CONTENT_TYPES,
  AVATAR_MAX_SIZE_BYTES,
  ProfileDetails,
  buildAvatarBlobFile,
  buildUpdateCurrentUserCommand,
  validateAvatarFile,
} from './profile.models';

function fakeFile(type: string, size: number, name = 'avatar.png'): File {
  const file = new File(['x'], name, { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

const details: ProfileDetails = {
  firstName: 'Ada',
  lastName: 'Lovelace',
  phoneNumber: '+420777123456',
  birthDate: new Date('1990-05-04T00:00:00Z'),
  languageCode: 'cs',
};

describe('validateAvatarFile', () => {
  it.each(AVATAR_ALLOWED_CONTENT_TYPES)('accepts %s', (contentType) => {
    expect(validateAvatarFile(fakeFile(contentType, 1024))).toEqual({
      valid: true,
    });
  });

  it('rejects a non-image file with a translated key', () => {
    expect(validateAvatarFile(fakeFile('application/pdf', 1024))).toEqual({
      valid: false,
      errorKey: 'pages.profile.avatar.invalid_type',
    });
  });

  it('rejects a file above the size limit with a translated key', () => {
    expect(
      validateAvatarFile(fakeFile('image/png', AVATAR_MAX_SIZE_BYTES + 1))
    ).toEqual({
      valid: false,
      errorKey: 'pages.profile.avatar.size_exceeded',
    });
  });

  it('accepts a file exactly on the size limit', () => {
    expect(
      validateAvatarFile(fakeFile('image/jpeg', AVATAR_MAX_SIZE_BYTES))
    ).toEqual({ valid: true });
  });

  it('reports the size problem first when a file is both too big and the wrong type', () => {
    expect(
      validateAvatarFile(fakeFile('text/plain', AVATAR_MAX_SIZE_BYTES + 1))
    ).toEqual({ valid: false, errorKey: 'pages.profile.avatar.size_exceeded' });
  });
});

describe('buildAvatarBlobFile', () => {
  it('carries the data URL, name and content type of the picked file', () => {
    const blob = buildAvatarBlobFile(
      fakeFile('image/webp', 10, 'me.webp'),
      'data:image/webp;base64,AAAA'
    );

    expect(blob).toBeInstanceOf(BlobFileDto);
    expect(blob.fileName).toBe('me.webp');
    expect(blob.base64Content).toBe('data:image/webp;base64,AAAA');
    expect(blob.contentType).toBe('image/webp');
  });
});

describe('buildUpdateCurrentUserCommand', () => {
  it('sends neither a photo nor a removal for an unchanged avatar', () => {
    const cmd = buildUpdateCurrentUserCommand(details, { kind: 'unchanged' });

    expect(cmd.photo).toBeUndefined();
    expect(cmd.removePhoto).toBe(false);
  });

  it('serializes an unchanged-avatar save without a photo or a removal flag', () => {
    const json = buildUpdateCurrentUserCommand(details, {
      kind: 'unchanged',
    }).toJSON();

    expect(json['photo']).toBeUndefined();
    expect(json['removePhoto']).toBe(false);
  });

  it('carries the profile details through unchanged', () => {
    const cmd = buildUpdateCurrentUserCommand(details, { kind: 'unchanged' });

    expect(cmd.firstName).toBe('Ada');
    expect(cmd.lastName).toBe('Lovelace');
    expect(cmd.phoneNumber).toBe('+420777123456');
    expect(cmd.birthDate).toEqual(new Date('1990-05-04T00:00:00Z'));
    expect(cmd.languageCode).toBe('cs');
  });

  it('sends the photo and no removal for an upload', () => {
    const photo = buildAvatarBlobFile(
      fakeFile('image/png', 10),
      'data:image/png;base64,AAAA'
    );

    const cmd = buildUpdateCurrentUserCommand(details, { kind: 'upload', photo });

    expect(cmd.photo).toBe(photo);
    expect(cmd.removePhoto).toBe(false);
  });

  it('sends the removal flag and no photo for a removal', () => {
    const cmd = buildUpdateCurrentUserCommand(details, { kind: 'remove' });

    expect(cmd.photo).toBeUndefined();
    expect(cmd.removePhoto).toBe(true);
  });
});
