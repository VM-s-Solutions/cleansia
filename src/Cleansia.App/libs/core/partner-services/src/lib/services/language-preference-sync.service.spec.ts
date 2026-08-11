import { TestBed } from '@angular/core/testing';
import {
  TranslateLoader,
  TranslateModule,
  TranslateService,
} from '@ngx-translate/core';
import { Observable, config, of, throwError } from 'rxjs';
import { SilentFailurePartnerClient } from '../client/base-client';
import { MyProfileDto, UpdateCurrentUserCommand } from '../client/partner-client';
import { PartnerAuthService } from './partner-auth.service';
import { PartnerLanguagePreferenceSyncService } from './language-preference-sync.service';

const stubLoader: TranslateLoader = {
  getTranslation: (): Observable<Record<string, unknown>> => of({}),
};

const storedProfile = MyProfileDto.fromJS({
  email: 'cleaner@cleansia.cz',
  firstName: 'Jana',
  lastName: 'Novakova',
  phoneNumber: '+420777123456',
  birthDate: '1990-05-15',
  preferredLanguageCode: 'en',
});

describe('PartnerLanguagePreferenceSyncService', () => {
  let getCurrent: jest.Mock;
  let updateCurrentUser: jest.Mock;
  let isLoggedIn: jest.Mock;
  let translate: TranslateService;
  let service: PartnerLanguagePreferenceSyncService;
  let unhandled: unknown[];
  let previousOnUnhandledError: ((error: unknown) => void) | null;

  const sentCommand = (): UpdateCurrentUserCommand =>
    updateCurrentUser.mock.calls[0][0];

  /**
   * RxJS reports an error nobody handled on a later macrotask rather than throwing at the call site,
   * so "the push did not throw" is not an assertion about anything. Recording it makes silence a
   * property the test can actually fail on.
   */
  const settle = (): Promise<void> =>
    new Promise((resolve) => setTimeout(() => resolve(), 0));

  beforeEach(() => {
    unhandled = [];
    previousOnUnhandledError = config.onUnhandledError;
    config.onUnhandledError = (error: unknown) => unhandled.push(error);
  });

  afterEach(() => {
    config.onUnhandledError = previousOnUnhandledError;
  });

  beforeEach(() => {
    getCurrent = jest.fn().mockReturnValue(of(storedProfile));
    updateCurrentUser = jest.fn().mockReturnValue(of({ id: 'user-1' }));
    isLoggedIn = jest.fn().mockReturnValue(true);

    TestBed.configureTestingModule({
      imports: [
        TranslateModule.forRoot({
          loader: { provide: TranslateLoader, useValue: stubLoader },
        }),
      ],
      providers: [
        {
          provide: SilentFailurePartnerClient,
          useValue: { userClient: { getCurrent, updateCurrentUser } },
        },
        { provide: PartnerAuthService, useValue: { isLoggedIn } },
      ],
    });

    translate = TestBed.inject(TranslateService);
    translate.addLangs(['cs', 'en', 'sk', 'uk', 'ru']);
    translate.setDefaultLang('en');
    // The app's APP_INITIALIZER picks the boot language before any component exists.
    translate.use('en');

    service = TestBed.inject(PartnerLanguagePreferenceSyncService);
  });

  it('pushes the picked language with the stored profile replayed alongside it', () => {
    service.start();

    translate.use('cs');

    expect(getCurrent).toHaveBeenCalledTimes(1);
    expect(sentCommand()).toBeInstanceOf(UpdateCurrentUserCommand);
    expect(sentCommand().toJSON()).toEqual({
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

  it('makes no request at all when nobody is signed in', () => {
    isLoggedIn.mockReturnValue(false);
    service.start();

    translate.use('cs');

    expect(getCurrent).not.toHaveBeenCalled();
    expect(updateCurrentUser).not.toHaveBeenCalled();
    expect(translate.currentLang).toBe('cs');
  });

  it('leaves the picked language applied when the push fails, and reports nothing', async () => {
    updateCurrentUser.mockReturnValue(throwError(() => new Error('500')));
    service.start();

    translate.use('sk');
    await settle();

    expect(unhandled).toEqual([]);
    expect(updateCurrentUser).toHaveBeenCalledTimes(1);
    expect(translate.currentLang).toBe('sk');
  });

  it('leaves the picked language applied when the profile read fails', async () => {
    getCurrent.mockReturnValue(throwError(() => new Error('500')));
    service.start();

    translate.use('uk');
    await settle();

    expect(unhandled).toEqual([]);
    expect(updateCurrentUser).not.toHaveBeenCalled();
    expect(translate.currentLang).toBe('uk');
  });

  it('ignores the boot language, because it subscribes only once the app is running', () => {
    translate.use('ru');

    expect(getCurrent).not.toHaveBeenCalled();

    service.start();

    expect(getCurrent).not.toHaveBeenCalled();
  });

  it('pushes once per change however often it is started', () => {
    service.start();
    service.start();

    translate.use('cs');

    expect(updateCurrentUser).toHaveBeenCalledTimes(1);
  });
});
