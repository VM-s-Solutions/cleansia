import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APIBASEURL } from '@cleansia/partner-services';
import { HttpErrorInterceptorFn } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Actions } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { MessageService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';
import { readFileSync } from 'fs';
import { join } from 'path';
import { EMPTY } from 'rxjs';
import { OrderDetailsFacade } from './order-details.facade';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(
  __dirname,
  '../../../../../../apps/cleansia-partner.app/src/assets/i18n'
);
const ORDER_ID = 'ord-1';

/** The two refusals a cleaner meets at the door that name a rule rather than a state. */
const REFUSALS = [
  { field: 'PaymentType', code: 'order.cash_not_allowed_on_card_order' },
  { field: 'PaymentStatus', code: 'credit.cash_not_collectable_on_credit_order' },
] as const;

const bundleFor = (locale: string): Record<string, unknown> =>
  JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

/** The blob read resolves on the FileReader's load event, which is a macrotask behind the flush. */
const flushAsyncErrorHandling = async (): Promise<void> => {
  for (let tick = 0; tick < 5; tick++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
};

/**
 * The real snackbar over a spied MessageService, so the assertion is on the toast the cleaner is
 * left reading. `showSnackbar` clears the queue before it adds, so a second toast from the facade
 * replaced the interceptor's translated sentence with the raw code.
 */
describe.each(LOCALES)('a refused cash collection, read in %s', (locale) => {
  let add: jest.Mock;
  let httpMock: HttpTestingController;
  let facade: OrderDetailsFacade;
  let translate: TranslateService;

  beforeEach(() => {
    add = jest.fn();
    TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [
        provideHttpClient(withInterceptors([HttpErrorInterceptorFn])),
        provideHttpClientTesting(),
        { provide: APIBASEURL, useValue: '' },
        OrderDetailsFacade,
        { provide: MessageService, useValue: { add, clear: jest.fn() } },
        { provide: DialogService, useValue: { open: jest.fn() } },
        { provide: Store, useValue: { dispatch: jest.fn() } },
        { provide: Actions, useValue: EMPTY },
      ],
    });

    translate = TestBed.inject(TranslateService);
    translate.setTranslation(locale, bundleFor(locale));
    translate.use(locale);

    httpMock = TestBed.inject(HttpTestingController);
    facade = TestBed.inject(OrderDetailsFacade);
  });

  afterEach(() => httpMock.verify());

  it.each(REFUSALS)('shows $code as its sentence, never as the raw code', async ({ field, code }) => {
    facade.markCashCollected(ORDER_ID);

    httpMock.expectOne('/api/Order/MarkCashCollected').flush(
      new Blob([JSON.stringify({ errors: { [field]: code } })], {
        type: 'application/json',
      }),
      { status: 400, statusText: 'Bad Request' }
    );
    await flushAsyncErrorHandling();

    httpMock
      .expectOne((request) => request.url.startsWith('/api/Order/GetById'))
      .flush(new Blob([JSON.stringify({ id: ORDER_ID })], { type: 'application/json' }));
    await flushAsyncErrorHandling();

    const sentence = translate.instant(`api.${code}`);
    const details = add.mock.calls.map(([message]) => message.detail as string);

    expect(sentence).not.toBe(`api.${code}`);
    expect(details[details.length - 1]).toBe(sentence);
    expect(details.filter((detail) => detail.includes(code))).toEqual([]);
    expect(facade.loading()).toBe(false);
  });
});
