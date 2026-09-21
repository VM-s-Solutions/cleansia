import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminAuthService,
  AdminClient,
  ADMINAPIBASEURL,
  AdminRole,
  AdminUserDetailDto,
} from '@cleansia/admin-services';
import { HttpErrorInterceptorFn, SnackbarService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { AdminUserFormFacade } from './admin-user-form.facade';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(__dirname, '../../../../../../apps/cleansia-admin.app/src/assets/i18n');
const REFUSALS = [
  'admin_user.cannot_demote_last_administrator',
  'admin_user.cannot_change_own_role',
] as const;
const GENERIC_FALLBACK_KEY = 'api.common.error_occurred';

const bundleFor = (locale: string): Record<string, unknown> =>
  JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

/** The blob read resolves on the FileReader's load event, which is a macrotask behind the flush. */
const flushAsyncErrorHandling = async (): Promise<void> => {
  for (let tick = 0; tick < 5; tick++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
};

/**
 * The last-Administrator guard runs under the company's lock on the server and only there; the
 * web learns of it as a code. The interceptor substitutes a generic sentence whenever `instant`
 * echoes the key back, so an untranslated refusal reads as "An error occurred" and the
 * administrator retries what can never succeed.
 */
describe('role assignment refusals', () => {
  it('every admin locale carries both refusal sentences', () => {
    for (const locale of LOCALES) {
      const api = bundleFor(locale)['api'] as Record<string, Record<string, string>>;

      for (const code of REFUSALS) {
        const [group, key] = code.split('.');
        expect(api[group][key]).toBeTruthy();
      }
    }
  });

  describe.each(LOCALES)('rendered through the interceptor (%s)', (locale) => {
    let showError: jest.Mock;
    let httpMock: HttpTestingController;
    let facade: AdminUserFormFacade;
    let translate: TranslateService;
    let control: FormControl<AdminRole | null>;

    beforeEach(async () => {
      showError = jest.fn();
      TestBed.configureTestingModule({
        imports: [TranslateModule.forRoot()],
        providers: [
          provideHttpClient(withInterceptors([HttpErrorInterceptorFn])),
          provideHttpClientTesting(),
          { provide: ADMINAPIBASEURL, useValue: '' },
          AdminClient,
          AdminUserFormFacade,
          { provide: AdminAuthService, useValue: { getUserId: () => 'usr-me' } },
          { provide: Router, useValue: { navigate: jest.fn() } },
          {
            provide: SnackbarService,
            useValue: {
              showError,
              showSuccess: jest.fn(),
              showSuccessTranslated: jest.fn(),
            },
          },
        ],
      });

      translate = TestBed.inject(TranslateService);
      translate.setTranslation(locale, bundleFor(locale));
      translate.use(locale);

      httpMock = TestBed.inject(HttpTestingController);
      facade = TestBed.inject(AdminUserFormFacade);
      control = new FormControl<AdminRole | null>(null);
      facade.connectRoleControl(control);

      facade.loadUser('usr-1');
      httpMock.expectOne('/api/AdminUser/details/usr-1').flush(
        new Blob(
          [JSON.stringify(AdminUserDetailDto.fromJS({ id: 'usr-1', adminRole: AdminRole.Administrator }).toJSON())],
          { type: 'application/json' }
        )
      );
      await flushAsyncErrorHandling();
      expect(control.value).toBe(AdminRole.Administrator);
    });

    afterEach(() => httpMock.verify());

    it('shows the last-Administrator sentence once, not the generic fallback, and steps the picker back', async () => {
      control.setValue(AdminRole.Support);

      httpMock.expectOne('/api/AdminUser/usr-1/role').flush(
        new Blob([JSON.stringify({ errors: { Role: REFUSALS[0] } })], {
          type: 'application/json',
        }),
        { status: 400, statusText: 'Bad Request' }
      );
      await flushAsyncErrorHandling();

      const expected = translate.instant(`api.${REFUSALS[0]}`);
      expect(expected).not.toBe(`api.${REFUSALS[0]}`);
      expect(showError).toHaveBeenCalledTimes(1);
      expect(showError).toHaveBeenCalledWith(expected);
      expect(showError).not.toHaveBeenCalledWith(translate.instant(GENERIC_FALLBACK_KEY));
      expect(control.value).toBe(AdminRole.Administrator);
    });
  });
});
