import {
  AddSavedAddressCommand,
  BlobFileDto,
  ChangePasswordCommand,
  UpdateSavedAddressCommand,
} from '@cleansia/customer-services';
import {
  AVATAR_ALLOWED_CONTENT_TYPES,
  AVATAR_MAX_SIZE_BYTES,
  ProfileDetails,
  SavedAddressFields,
  buildAddSavedAddressCommand,
  buildAvatarBlobFile,
  buildChangePasswordCommand,
  buildCreditRow,
  buildUpdateCurrentUserCommand,
  buildUpdateSavedAddressCommand,
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

  it('rejects svg — it is script-capable and the platform will not serve it as an image', () => {
    expect(validateAvatarFile(fakeFile('image/svg+xml', 1024))).toEqual({
      valid: false,
      errorKey: 'pages.profile.avatar.invalid_type',
    });
  });

  // The backend would store these, but ServedContentType can only hand them back as
  // octet-stream and no desktop browser renders a tiff — the avatar would never appear.
  it.each(['image/bmp', 'image/tiff'])(
    'rejects %s rather than accepting an upload that can never render',
    (contentType) => {
      expect(validateAvatarFile(fakeFile(contentType, 1024))).toEqual({
        valid: false,
        errorKey: 'pages.profile.avatar.invalid_type',
      });
    }
  );

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

// Every member of a generated command is optional, so a dropped assignment type-checks.
// These pin the serialized body instead (ADR-0031).
describe('command bodies on the wire', () => {
  const addressFields: SavedAddressFields = {
    label: 'Home',
    street: 'Vodickova 12',
    city: 'Praha',
    zipCode: '11000',
    countryId: 'cz',
    latitude: 50.08,
    longitude: 14.42,
  };

  it('serializes a password change with the email, a blank code and the new password', () => {
    const command = buildChangePasswordCommand('ada@example.com', 'Heslo1234');

    expect(command).toBeInstanceOf(ChangePasswordCommand);
    expect(command.toJSON()).toEqual({
      email: 'ada@example.com',
      code: '',
      newPassword: 'Heslo1234',
    });
  });

  it('serializes a new saved address with its coordinates and the default flag', () => {
    const command = buildAddSavedAddressCommand(addressFields, true);

    expect(command).toBeInstanceOf(AddSavedAddressCommand);
    expect(command.toJSON()).toEqual({
      label: 'Home',
      street: 'Vodickova 12',
      city: 'Praha',
      zipCode: '11000',
      countryId: 'cz',
      latitude: 50.08,
      longitude: 14.42,
      setAsDefault: true,
    });
  });

  it('serializes an edited saved address with its id and no default flag', () => {
    const command = buildUpdateSavedAddressCommand('addr-1', addressFields);

    expect(command).toBeInstanceOf(UpdateSavedAddressCommand);
    expect(command.toJSON()).toEqual({
      savedAddressId: 'addr-1',
      label: 'Home',
      street: 'Vodickova 12',
      city: 'Praha',
      zipCode: '11000',
      countryId: 'cz',
      latitude: 50.08,
      longitude: 14.42,
    });
  });
});

describe('buildCreditRow', () => {
  it('shows a ZERO balance rather than hiding the row', () => {
    // The whole point of the owner's remark: a customer with no credit could not tell "you have
    // none" from "this screen forgot about credit".
    const row = buildCreditRow({ balance: 0, currencyCode: 'CZK', expiresOn: null });

    expect(row).toEqual({ amount: 0, currency: 'CZK', expiresOn: null });
  });

  it('shows a positive balance', () => {
    const expires = new Date('2027-01-31T00:00:00Z');
    const row = buildCreditRow({ balance: 350, currencyCode: 'CZK', expiresOn: expires });

    expect(row).toEqual({ amount: 350, currency: 'CZK', expiresOn: expires });
  });

  it('omits the row when the read FAILED, which is the only thing null means', () => {
    // Not the same as zero: claiming a balance nobody verified is worse than saying nothing.
    expect(buildCreditRow(null)).toBeNull();
  });

  it('falls back to an empty currency rather than rendering undefined next to the amount', () => {
    expect(buildCreditRow({ balance: 0, currencyCode: null, expiresOn: null })?.currency).toBe('');
  });

  it('carries no expiry when there is nothing to expire', () => {
    expect(buildCreditRow({ balance: 0, currencyCode: 'CZK' })?.expiresOn).toBeNull();
  });
});
