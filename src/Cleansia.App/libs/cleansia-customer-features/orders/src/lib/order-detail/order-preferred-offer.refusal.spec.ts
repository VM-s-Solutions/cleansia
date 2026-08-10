import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CUSTOMER_API_BASE_URL,
  OrderItem,
  OrderStatus,
  PreferredOfferState,
} from '@cleansia/customer-services';
import { HttpErrorInterceptorFn, SnackbarService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(
  __dirname,
  '../../../../../../apps/cleansia.app/src/assets/i18n'
);
const ERROR_CODE = 'order.preferred_offer_closed';
const GENERIC_FALLBACK_KEY = 'api.common.error_occurred';

const bundleFor = (locale: string): Record<string, unknown> =>
  JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

/**
 * jsdom ships no `Blob.prototype.text`, and the generated clients read errors as blobs
 * (`responseType: 'blob'`), so without this the interceptor's real branch takes its `.catch` and
 * every refusal reads as the generic fallback — a test that agrees with the bug it is checking for.
 */
beforeAll(() => {
  if (typeof Blob.prototype.text !== 'function') {
    Blob.prototype.text = function (this: Blob): Promise<string> {
      return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(reader.error);
        reader.readAsText(this);
      });
    };
  }
});

/** The blob read resolves on the FileReader's load event, which is a macrotask behind the flush. */
const flushAsyncErrorHandling = async (): Promise<void> => {
  for (let tick = 0; tick < 5; tick++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
};

function buildOrder(): OrderItem {
  return OrderItem.fromJS({
    id: 'ord-1',
    orderStatus: { value: OrderStatus.New, name: 'New' },
    cleaningDateTime: '2026-09-01T08:00:00Z',
    selectedServices: [{ id: 'svc-1' }],
    selectedPackages: [],
    preferredOffer: {
      state: PreferredOfferState.Closed,
      cleanerName: 'Anna Nováková',
      canChooseAnother: true,
    },
  });
}

/**
 * Every structural refusal of a re-choose collapses to one key — naming the reason would let the
 * customer tell "someone else is still considering it" from "your favourite passed", which is
 * exactly what no surface may say. So the one key has to render, in every locale: without a
 * translation the interceptor substitutes a generic sentence and the customer reads a network blip.
 */
describe('preferred-cleaner re-choose refusal', () => {
  it('every customer locale carries the closure sentence', () => {
    for (const locale of LOCALES) {
      const api = bundleFor(locale)['api'] as Record<
        string,
        Record<string, string>
      >;

      expect(api['order']['preferred_offer_closed']).toBeTruthy();
    }
  });

  describe.each(LOCALES)('rendered through the interceptor (%s)', (locale) => {
    let showError: jest.Mock;
    let httpMock: HttpTestingController;
    let facade: OrderPreferredOfferFacade;
    let translate: TranslateService;

    beforeEach(() => {
      showError = jest.fn();
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [TranslateModule.forRoot()],
        providers: [
          provideHttpClient(withInterceptors([HttpErrorInterceptorFn])),
          provideHttpClientTesting(),
          { provide: CUSTOMER_API_BASE_URL, useValue: '' },
          { provide: PLATFORM_ID, useValue: 'browser' },
          OrderPreferredOfferFacade,
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
      facade = TestBed.inject(OrderPreferredOfferFacade);
      facade.connect({
        order: signal<OrderItem | null>(buildOrder()),
        onChosen: jest.fn(),
      });
    });

    afterEach(() => httpMock.verify());

    it('shows the closure sentence, not the generic fallback', async () => {
      facade.select('emp-9');
      facade.submit();

      httpMock.expectOne('/api/Order/ChoosePreferredCleaner').flush(
        new Blob([JSON.stringify({ errors: { EmployeeId: ERROR_CODE } })], {
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
      expect(facade.submitting()).toBe(false);
    });
  });
});
