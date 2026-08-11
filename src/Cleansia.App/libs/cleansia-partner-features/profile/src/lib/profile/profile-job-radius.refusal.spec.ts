import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APIBASEURL, EmployeeItem } from '@cleansia/partner-services';
import { HttpErrorInterceptorFn, SnackbarService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { ProfileJobRadiusFacade } from './profile-job-radius.facade';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(
  __dirname,
  '../../../../../../apps/cleansia-partner.app/src/assets/i18n'
);
const ERROR_CODE = 'employee.job_radius_out_of_range';
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
 * The one bound the client does not enforce is the server's, and it arrives as a code. The
 * interceptor substitutes a generic sentence whenever `instant` echoes the key back, so a missing
 * translation reads as "An error occurred" and looks like a network blip rather than a bad number.
 */
describe('job radius refusal', () => {
  it('every partner locale carries the refusal sentence', () => {
    for (const locale of LOCALES) {
      const api = bundleFor(locale)['api'] as Record<
        string,
        Record<string, string>
      >;

      expect(api['employee']['job_radius_out_of_range']).toBeTruthy();
    }
  });

  describe.each(LOCALES)('rendered through the interceptor (%s)', (locale) => {
    let showError: jest.Mock;
    let httpMock: HttpTestingController;
    let facade: ProfileJobRadiusFacade;
    let translate: TranslateService;

    beforeEach(() => {
      showError = jest.fn();
      TestBed.configureTestingModule({
        imports: [TranslateModule.forRoot()],
        providers: [
          provideHttpClient(withInterceptors([HttpErrorInterceptorFn])),
          provideHttpClientTesting(),
          { provide: APIBASEURL, useValue: '' },
          ProfileJobRadiusFacade,
          {
            provide: SnackbarService,
            useValue: { showError, showSuccess: jest.fn() },
          },
        ],
      });

      translate = TestBed.inject(TranslateService);
      translate.setTranslation(locale, bundleFor(locale));
      translate.use(locale);

      httpMock = TestBed.inject(HttpTestingController);
      facade = TestBed.inject(ProfileJobRadiusFacade);
      facade.seed(EmployeeItem.fromJS({ id: 'emp-1', jobRadiusKm: 30 }));
    });

    afterEach(() => httpMock.verify());

    it('shows the out-of-range sentence, not the generic fallback', async () => {
      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '500' });
      facade.onSubmit();

      httpMock.expectOne('/api/Employee/UpdateJobRadius').flush(
        new Blob([JSON.stringify({ errors: { RadiusKm: ERROR_CODE } })], {
          type: 'application/json',
        }),
        { status: 400, statusText: 'Bad Request' }
      );
      await flushAsyncErrorHandling();

      const expected = translate.instant(`api.${ERROR_CODE}`);
      expect(expected).not.toBe(`api.${ERROR_CODE}`);
      expect(showError).toHaveBeenCalledWith(expected);
      expect(showError).not.toHaveBeenCalledWith(
        translate.instant(GENERIC_FALLBACK_KEY)
      );
    });
  });
});
