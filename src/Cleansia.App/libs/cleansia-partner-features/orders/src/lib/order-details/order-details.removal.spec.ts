import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APIBASEURL } from '@cleansia/partner-services';
import { HttpErrorInterceptorFn, SUPPRESS_ERROR_TOAST } from '@cleansia/services';
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
const TEMPLATE = join(__dirname, 'order-details.component.html');
const ORDER_ID = 'ord-1';
const REASON = 'The customer asked for a partner who speaks German';

const isRemovalRead = (request: { url: string }): boolean =>
  request.url.startsWith('/api/Order/GetMyAssignmentRemoval');

const blob = (body: unknown): Blob =>
  new Blob([JSON.stringify(body)], { type: 'application/json' });

const refused = (request: TestRequest, code: string): void =>
  request.flush(blob({ errors: { OrderId: code } }), {
    status: 400,
    statusText: 'Bad Request',
  });

/** The blob read resolves on the FileReader's load event, which is a macrotask behind the flush. */
const flushAsyncErrorHandling = async (): Promise<void> => {
  for (let tick = 0; tick < 5; tick++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
};

/**
 * A reassignment swaps the cleaner for another, so the job keeps whatever seats it had free. With none
 * left the order answers `order.not_found`; with one still open the removed cleaner browses it like any
 * other job. Either way the page asks the server why; the reason is never carried anywhere else.
 */
describe('the order a cleaner was taken off', () => {
  let add: jest.Mock;
  let httpMock: HttpTestingController;
  let facade: OrderDetailsFacade;

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

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      JSON.parse(readFileSync(join(I18N_DIR, 'en.json'), 'utf8'))
    );
    translate.use('en');

    httpMock = TestBed.inject(HttpTestingController);
    facade = TestBed.inject(OrderDetailsFacade);
  });

  afterEach(() => httpMock.verify());

  const orderRefused = async (code: string): Promise<void> => {
    facade.loadOrderDetails(ORDER_ID);
    refused(
      httpMock.expectOne((request) => request.url.startsWith('/api/Order/GetById')),
      code
    );
    await flushAsyncErrorHandling();
  };

  const orderOpened = async (order: object): Promise<void> => {
    facade.loadOrderDetails(ORDER_ID);
    httpMock
      .expectOne((request) => request.url.startsWith('/api/Order/GetById'))
      .flush(blob(order));
    await flushAsyncErrorHandling();
  };

  it("shows the administrator's reason, read for this order", async () => {
    await orderRefused('order.not_found');

    const request = httpMock.expectOne(isRemovalRead);
    expect(request.request.urlWithParams).toContain(`OrderId=${ORDER_ID}`);
    request.flush(
      blob({ orderId: ORDER_ID, reason: REASON, removedOn: '2026-09-28T10:00:00Z' })
    );
    await flushAsyncErrorHandling();

    expect(facade.removal()?.reason).toBe(REASON);
  });

  it('asks without the shared error toast, so a cleaner never removed sees one toast, not two', async () => {
    await orderRefused('order.not_found');
    const toastsBefore = add.mock.calls.length;
    expect(toastsBefore).toBe(1);

    const request = httpMock.expectOne(isRemovalRead);
    expect(request.request.context.get(SUPPRESS_ERROR_TOAST)).toBe(true);
    refused(request, 'order.not_found');
    await flushAsyncErrorHandling();

    expect(add.mock.calls.length).toBe(toastsBefore);
    expect(facade.removal()).toBeNull();
    expect(facade.error()).not.toBeNull();
  });

  it('asks nothing when the order fails for another reason', async () => {
    await orderRefused('validation.required');

    httpMock.expectNone(isRemovalRead);
    expect(facade.removal()).toBeNull();
  });

  it('shows the reason on a job that still has a seat open, over the job itself', async () => {
    await orderOpened({ id: ORDER_ID, isAssignedToCurrentUser: false });

    const request = httpMock.expectOne(isRemovalRead);
    expect(request.request.urlWithParams).toContain(`OrderId=${ORDER_ID}`);
    request.flush(
      blob({ orderId: ORDER_ID, reason: REASON, removedOn: '2026-09-28T10:00:00Z' })
    );
    await flushAsyncErrorHandling();

    expect(facade.removal()?.reason).toBe(REASON);
    expect(facade.orderDetails()?.id).toBe(ORDER_ID);
    expect(facade.error()).toBeNull();
  });

  it('shows no toast to a cleaner browsing a job nobody took them off', async () => {
    await orderOpened({ id: ORDER_ID, isAssignedToCurrentUser: false });

    refused(httpMock.expectOne(isRemovalRead), 'order.not_found');
    await flushAsyncErrorHandling();

    expect(add).not.toHaveBeenCalled();
    expect(facade.removal()).toBeNull();
    expect(facade.error()).toBeNull();
  });

  it('asks nothing about a job the cleaner is on', async () => {
    await orderOpened({ id: ORDER_ID, isAssignedToCurrentUser: true });

    httpMock.expectNone(isRemovalRead);
    expect(facade.removal()).toBeNull();
  });

  it('forgets an earlier removal when the order is loaded again', async () => {
    await orderRefused('order.not_found');
    httpMock
      .expectOne(isRemovalRead)
      .flush(blob({ orderId: ORDER_ID, reason: REASON, removedOn: '2026-09-28T10:00:00Z' }));
    await flushAsyncErrorHandling();

    await orderOpened({ id: ORDER_ID, isAssignedToCurrentUser: true });

    expect(facade.removal()).toBeNull();
  });
});

describe('the removal copy', () => {
  const orderDetailsCopy = (locale: string): Record<string, string> =>
    JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'))['pages'][
      'order_details'
    ];

  it.each(LOCALES)('%s says the cleaner was taken off and carries the reason', (locale) => {
    const copy = orderDetailsCopy(locale);

    expect(copy['removed_title']).toBeTruthy();
    expect(copy['removed_message']).toContain('{{reason}}');
  });

  it('fills the reason from the removal the page read, on the closed page and over an open job', () => {
    const template = readFileSync(TEMPLATE, 'utf8');

    expect(template).toContain(
      "'pages.order_details.removed_message' | translate: { reason: removalReason() }"
    );
    expect(template).toContain('@if (removalReason(); as reason)');
    expect(template).toContain(
      "'pages.order_details.removed_message' | translate: { reason: reason }"
    );
  });
});
