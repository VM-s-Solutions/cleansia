import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HttpErrorInterceptorFn, SnackbarService } from '@cleansia/services';
import {
  TranslateLoader,
  TranslateModule,
  TranslateService,
} from '@ngx-translate/core';
import { Observable, of } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import { APIBASEURL } from '../client/partner-client';
import { PartnerAuthService } from './partner-auth.service';
import { PartnerLanguagePreferenceSyncService } from './language-preference-sync.service';

const CURRENT_URL = '/api/User/GetCurrent';
const UPDATE_URL = '/api/User/UpdateCurrentUser';

const stubLoader: TranslateLoader = {
  getTranslation: (): Observable<Record<string, unknown>> => of({}),
};

const storedProfile = {
  firstName: 'Jana',
  lastName: 'Novakova',
  phoneNumber: '+420777123456',
  preferredLanguageCode: 'en',
};

const refusal = (): Blob =>
  new Blob([JSON.stringify({ errors: { LanguageCode: 'user.not_found' } })], {
    type: 'application/json',
  });

/** The interceptor reads a blob body on a FileReader load event, a macrotask behind the flush. */
const settle = async (): Promise<void> => {
  for (let tick = 0; tick < 5; tick++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
};

/**
 * The service promises the cleaner never sees a failed language push, but `HttpErrorInterceptorFn`
 * fires on every non-404/403 error and knows nothing about that promise — so the facade was silent
 * and the interceptor was not. Both requests of the push opt out; nothing else does.
 */
describe('a failed language push is silent all the way to the snackbar', () => {
  let showError: jest.Mock;
  let httpMock: HttpTestingController;
  let translate: TranslateService;
  let partnerClient: PartnerClient;
  let service: PartnerLanguagePreferenceSyncService;

  beforeEach(() => {
    showError = jest.fn();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [
        TranslateModule.forRoot({
          loader: { provide: TranslateLoader, useValue: stubLoader },
        }),
      ],
      providers: [
        provideHttpClient(withInterceptors([HttpErrorInterceptorFn])),
        provideHttpClientTesting(),
        { provide: APIBASEURL, useValue: '' },
        { provide: SnackbarService, useValue: { showError } },
        { provide: PartnerAuthService, useValue: { isLoggedIn: () => true } },
      ],
    });

    translate = TestBed.inject(TranslateService);
    translate.addLangs(['cs', 'en']);
    translate.setDefaultLang('en');
    translate.use('en');

    httpMock = TestBed.inject(HttpTestingController);
    partnerClient = TestBed.inject(PartnerClient);
    service = TestBed.inject(PartnerLanguagePreferenceSyncService);
  });

  afterEach(() => httpMock.verify());

  it('raises no snackbar when the push itself is refused', async () => {
    service.start();
    translate.use('cs');

    httpMock
      .expectOne(CURRENT_URL)
      .flush(
        new Blob([JSON.stringify(storedProfile)], { type: 'application/json' }),
        { status: 200, statusText: 'OK' }
      );
    await settle();
    httpMock
      .expectOne(UPDATE_URL)
      .flush(refusal(), { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(showError).not.toHaveBeenCalled();
    expect(translate.currentLang).toBe('cs');
  });

  it('raises no snackbar when the profile read that precedes it is refused', async () => {
    service.start();
    translate.use('cs');

    httpMock
      .expectOne(CURRENT_URL)
      .flush(refusal(), { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(showError).not.toHaveBeenCalled();
    expect(translate.currentLang).toBe('cs');
  });

  it('leaves every other call on the same endpoint toasting, so the opt-out is per request', async () => {
    partnerClient.userClient.getCurrent().subscribe({ error: () => undefined });

    httpMock
      .expectOne(CURRENT_URL)
      .flush(refusal(), { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(showError).toHaveBeenCalledTimes(1);
  });
});
