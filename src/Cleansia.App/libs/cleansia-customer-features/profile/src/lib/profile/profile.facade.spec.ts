import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  MyProfileDto,
  UpdateCurrentUserCommand,
} from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { ProfileFacade } from './profile.facade';
import { ProfileDetails } from './profile.models';

function profileWithPhoto(
  fileName: string | null,
  blobUrl: string | null
): MyProfileDto {
  return MyProfileDto.fromJS({
    email: 'ada@example.com',
    firstName: 'Ada',
    lastName: 'Lovelace',
    phoneNumber: '+420777123456',
    birthDate: '1990-05-04',
    preferredLanguageCode: 'cs',
    profilePhoto: fileName ? { fileName, blobUrl } : null,
  });
}

function pngFile(name = 'avatar.png'): File {
  return new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], name, {
    type: 'image/png',
  });
}

const formDetails: ProfileDetails = {
  firstName: 'Ada',
  lastName: 'Lovelace',
  phoneNumber: '+420777123456',
  birthDate: new Date('1990-05-04T00:00:00Z'),
  languageCode: 'en',
};

describe('ProfileFacade', () => {
  let facade: ProfileFacade;
  let userClient: { getCurrent: jest.Mock; updateCurrentUser: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const lastCommand = (): UpdateCurrentUserCommand =>
    userClient.updateCurrentUser.mock.calls.at(-1)?.[0];

  beforeEach(() => {
    userClient = { getCurrent: jest.fn(), updateCurrentUser: jest.fn() };
    snackbar = {
      showSuccess: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        ProfileFacade,
        {
          provide: CustomerClient,
          useValue: { userClient, countryClient: { getServiced: jest.fn() } },
        },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
        {
          provide: SavedAddressStore,
          useValue: {
            addresses: signal([]),
            loading: signal(false),
            loaded: () => true,
          },
        },
      ],
    });

    facade = TestBed.inject(ProfileFacade);
  });

  describe('loading the profile', () => {
    it('renders the avatar from the profile photo reference', () => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=one'))
      );

      facade.loadProfile();

      expect(facade.avatarUrl()).toBe('https://blob/blob-1?sig=one');
      expect(facade.loading()).toBe(false);
    });

    it('leaves the avatar empty when the user has no photo', () => {
      userClient.getCurrent.mockReturnValue(of(profileWithPhoto(null, null)));

      facade.loadProfile();

      expect(facade.avatarUrl()).toBeNull();
    });

    it('leaves the avatar empty when the reference carries no url', () => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', null))
      );

      facade.loadProfile();

      expect(facade.avatarUrl()).toBeNull();
    });

    it('exposes the loading state while the profile is in flight', () => {
      const response$ = new Subject<MyProfileDto>();
      userClient.getCurrent.mockReturnValue(response$.asObservable());

      facade.loadProfile();
      expect(facade.loading()).toBe(true);

      response$.next(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=one'));
      response$.complete();
      expect(facade.loading()).toBe(false);
    });

    it('clears the loading state when the profile read fails', () => {
      userClient.getCurrent.mockReturnValue(throwError(() => new Error('x')));

      facade.loadProfile();

      expect(facade.loading()).toBe(false);
      expect(facade.user()).toBeNull();
      expect(facade.avatarUrl()).toBeNull();
    });
  });

  describe('the avatar url is keyed on the file name, not the signed url', () => {
    it('keeps the rendered url when a re-read returns the same file with a fresh signature', () => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=one'))
      );
      facade.loadProfile();

      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=two'))
      );
      facade.loadProfile();

      expect(facade.avatarUrl()).toBe('https://blob/blob-1?sig=one');
    });

    it('adopts the new url when the file name changes', () => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=one'))
      );
      facade.loadProfile();

      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-2', 'https://blob/blob-2?sig=two'))
      );
      facade.loadProfile();

      expect(facade.avatarUrl()).toBe('https://blob/blob-2?sig=two');
    });
  });

  describe('image errors re-read the profile once', () => {
    it('re-reads the profile and adopts the fresh signature for the same file', () => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=expired'))
      );
      facade.loadProfile();

      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=fresh'))
      );
      facade.onAvatarLoadFailed();

      expect(userClient.getCurrent).toHaveBeenCalledTimes(2);
      expect(facade.avatarUrl()).toBe('https://blob/blob-1?sig=fresh');
    });

    it('falls back to the placeholder instead of re-reading a second time', () => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=gone'))
      );
      facade.loadProfile();

      facade.onAvatarLoadFailed();
      facade.onAvatarLoadFailed();

      expect(userClient.getCurrent).toHaveBeenCalledTimes(2);
      expect(facade.avatarUrl()).toBeNull();
    });

    it('re-arms the retry once an image renders', () => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=one'))
      );
      facade.loadProfile();

      facade.onAvatarLoadFailed();
      facade.onAvatarLoaded();
      facade.onAvatarLoadFailed();

      expect(userClient.getCurrent).toHaveBeenCalledTimes(3);
      expect(facade.avatarUrl()).not.toBeNull();
    });
  });

  describe('uploading an avatar', () => {
    beforeEach(() => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto(null, null))
      );
      facade.loadProfile();
    });

    it('sends the picked image and never a removal', async () => {
      userClient.updateCurrentUser.mockReturnValue(of({ id: 'user-1' }));
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-9', 'https://blob/blob-9?sig=new'))
      );

      await facade.uploadAvatar(pngFile('me.png'));

      const command = lastCommand();
      expect(command.photo?.fileName).toBe('me.png');
      expect(command.photo?.contentType).toBe('image/png');
      expect(command.photo?.base64Content).toContain('base64,');
      expect(command.removePhoto).toBe(false);
      expect(facade.avatarSaving()).toBe(false);
    });

    it('re-reads the profile so the avatar updates without a page reload', async () => {
      userClient.updateCurrentUser.mockReturnValue(of({ id: 'user-1' }));
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-9', 'https://blob/blob-9?sig=new'))
      );

      await facade.uploadAvatar(pngFile());

      expect(facade.avatarUrl()).toBe('https://blob/blob-9?sig=new');
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.profile.avatar.upload_success'
      );
    });

    it('rejects a non-image before any request leaves the browser', async () => {
      await facade.uploadAvatar(
        new File(['x'], 'cv.pdf', { type: 'application/pdf' })
      );

      expect(userClient.updateCurrentUser).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.profile.avatar.invalid_type'
      );
    });

    it('rejects an oversized image before any request leaves the browser', async () => {
      const big = pngFile();
      Object.defineProperty(big, 'size', { value: 10 * 1024 * 1024 + 1 });

      await facade.uploadAvatar(big);

      expect(userClient.updateCurrentUser).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.profile.avatar.size_exceeded'
      );
    });

    it('clears the saving state and keeps the avatar when the upload fails', async () => {
      userClient.updateCurrentUser.mockReturnValue(
        throwError(() => new Error('x'))
      );

      await facade.uploadAvatar(pngFile());

      expect(facade.avatarSaving()).toBe(false);
      expect(userClient.getCurrent).toHaveBeenCalledTimes(1);
    });
  });

  describe('removing an avatar', () => {
    beforeEach(() => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=one'))
      );
      facade.loadProfile();
    });

    it('asks for removal and sends no photo', () => {
      userClient.updateCurrentUser.mockReturnValue(of({ id: 'user-1' }));
      userClient.getCurrent.mockReturnValue(of(profileWithPhoto(null, null)));

      facade.removeAvatar();

      const command = lastCommand();
      expect(command.removePhoto).toBe(true);
      expect(command.photo).toBeUndefined();
    });

    it('reverts to the placeholder once the profile is re-read', () => {
      userClient.updateCurrentUser.mockReturnValue(of({ id: 'user-1' }));
      userClient.getCurrent.mockReturnValue(of(profileWithPhoto(null, null)));

      facade.removeAvatar();

      expect(facade.avatarUrl()).toBeNull();
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.profile.avatar.remove_success'
      );
    });

    it('does nothing when there is no photo to remove', () => {
      userClient.getCurrent.mockReturnValue(of(profileWithPhoto(null, null)));
      facade.loadProfile();

      facade.removeAvatar();

      expect(userClient.updateCurrentUser).not.toHaveBeenCalled();
    });
  });

  describe('saving the profile details', () => {
    beforeEach(() => {
      userClient.getCurrent.mockReturnValue(
        of(profileWithPhoto('blob-1', 'https://blob/blob-1?sig=one'))
      );
      facade.loadProfile();
    });

    it('sends no photo and no removal, so an existing avatar survives', () => {
      userClient.updateCurrentUser.mockReturnValue(of({ id: 'user-1' }));

      facade.saveProfile(formDetails);

      const command = lastCommand();
      expect(command.photo).toBeUndefined();
      expect(command.removePhoto).toBe(false);
      expect(command.toJSON()['removePhoto']).toBe(false);
    });

    it('sends the edited details', () => {
      userClient.updateCurrentUser.mockReturnValue(of({ id: 'user-1' }));

      facade.saveProfile({ ...formDetails, firstName: 'Grace' });

      expect(lastCommand().firstName).toBe('Grace');
      expect(facade.saving()).toBe(false);
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.profile.save_success'
      );
    });

    it('reports a failed save and clears the saving state', () => {
      userClient.updateCurrentUser.mockReturnValue(
        throwError(() => new Error('x'))
      );

      facade.saveProfile(formDetails);

      expect(facade.saving()).toBe(false);
      expect(snackbar.showError).toHaveBeenCalledWith(
        'pages.profile.save_error'
      );
    });
  });
});
