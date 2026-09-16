import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminGdprClient,
  CustomerAuditClient,
  FileResponse,
  OrderItem,
  PagedDataOfTimelineEntryDto,
  TimelineEntryDto,
  TimelineSource,
  UserItem,
} from '@cleansia/admin-services';
import { FileDownloadService, PermissionService, Policy, PhysicalPolicy, resolvePhysicalPolicy, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { OrderDetailFacade } from './order-detail.facade';

describe('OrderDetailFacade', () => {
  let facade: OrderDetailFacade;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        OrderDetailFacade,
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: PermissionService, useValue: { hasPolicy: () => true } },
        { provide: AdminClient, useValue: { adminOrderClient: {} } },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn(), showError: jest.fn() } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: AdminGdprClient, useValue: { incidentFile: jest.fn() } },
        { provide: CustomerAuditClient, useValue: { timeline: jest.fn() } },
      ],
    });
    facade = TestBed.inject(OrderDetailFacade);
  });

  it('labels a price with the symbol the order carries', () => {
    facade.order.set(OrderItem.fromJS({ currency: { symbol: '€', code: 'EUR' } }));

    expect(facade.formatPrice(45)).toBe('45.00 €');
  });

  // The order always names its currency; a bare number is honest when it does not, "Kc" is a guess.
  it('prints a bare number rather than a currency the order does not name', () => {
    facade.order.set(OrderItem.fromJS({}));

    expect(facade.formatPrice(45)).toBe('45.00');
    expect(facade.formatPrice(null)).toBe('-');
  });
});

describe('OrderDetailFacade — incident file', () => {
  let facade: OrderDetailFacade;
  let auditClient: { timeline: jest.Mock };
  let gdprClient: { incidentFile: jest.Mock };
  let snackbar: {
    showApiError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showSuccessTranslated: jest.Mock;
  };
  let download: jest.Mock;

  const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
  const served: FileResponse = {
    data: pdf,
    status: 200,
    fileName: 'incident-cust-1-20260914.pdf',
  };

  function entry(
    source: TimelineSource,
    actorId: string | undefined,
    success: boolean
  ): TimelineEntryDto {
    return TimelineEntryDto.fromJS({
      source,
      id: `${source}-${actorId ?? 'guest'}-${success}`,
      occurredOn: '2026-09-14T10:00:00Z',
      actorId,
      action: 'x',
      resourceType: 'Order',
      resourceId: 'order-1',
      success,
    });
  }

  function page(
    entries: TimelineEntryDto[],
    total = entries.length
  ): PagedDataOfTimelineEntryDto {
    return PagedDataOfTimelineEntryDto.fromJS({ data: entries, total });
  }

  function adminRows(count: number): TimelineEntryDto[] {
    return Array.from({ length: count }, (_, i) =>
      entry(TimelineSource.Admin, `admin-${i}`, true)
    );
  }

  beforeEach(() => {
    auditClient = {
      timeline: jest.fn().mockReturnValue(
        of(
          page([
            entry(TimelineSource.Admin, 'admin-1', true),
            entry(TimelineSource.Customer, 'stranger-1', false),
            entry(TimelineSource.Customer, 'cust-1', true),
          ])
        )
      ),
    };
    gdprClient = { incidentFile: jest.fn().mockReturnValue(of(served)) };
    snackbar = {
      showApiError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };
    download = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        OrderDetailFacade,
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: PermissionService, useValue: { hasPolicy: () => true } },
        { provide: AdminClient, useValue: { adminOrderClient: {} } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: AdminGdprClient, useValue: gdprClient },
        { provide: CustomerAuditClient, useValue: auditClient },
        { provide: FileDownloadService, useValue: { downloadBlob: download } },
      ],
    });
    facade = TestBed.inject(OrderDetailFacade);
    facade.order.set(OrderItem.fromJS({ id: 'order-1' }));
  });

  it('reads the subject off the order trail and downloads the file scoped to this order', () => {
    facade.exportIncidentFile();

    expect(auditClient.timeline).toHaveBeenCalledWith(
      undefined,
      'Order',
      'order-1',
      undefined,
      0,
      100
    );
    expect(gdprClient.incidentFile).toHaveBeenCalledWith('cust-1', 'order-1');
    expect(download).toHaveBeenCalledWith(pdf, 'incident-cust-1-20260914.pdf');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.order_detail.incident_file.success'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('names the file by subject and UTC day when the server sends no file name', () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-09-14T23:30:00Z'));
    gdprClient.incidentFile.mockReturnValue(of({ data: pdf, status: 200 }));

    facade.exportIncidentFile();

    expect(download).toHaveBeenCalledWith(pdf, 'incident-cust-1-20260914.pdf');
    jest.useRealTimers();
  });

  // The trail is newest-first and the customer's own rows are the oldest on an order that admins
  // and cleaners have worked on since — every reveal and every drop lands above the booking — so a
  // subject-less first page is not a subject-less trail.
  it('pages on through the trail when the newest page names no customer', () => {
    auditClient.timeline
      .mockReturnValueOnce(of(page(adminRows(100), 150)))
      .mockReturnValueOnce(
        of(page([...adminRows(49), entry(TimelineSource.Customer, 'cust-1', true)], 150))
      );

    facade.exportIncidentFile();

    expect(auditClient.timeline).toHaveBeenCalledTimes(2);
    expect(auditClient.timeline).toHaveBeenNthCalledWith(
      1,
      undefined,
      'Order',
      'order-1',
      undefined,
      0,
      100
    );
    expect(auditClient.timeline).toHaveBeenNthCalledWith(
      2,
      undefined,
      'Order',
      'order-1',
      undefined,
      100,
      100
    );
    expect(gdprClient.incidentFile).toHaveBeenCalledWith('cust-1', 'order-1');
    expect(download).toHaveBeenCalledWith(pdf, 'incident-cust-1-20260914.pdf');
    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('stops at the end of the trail when no page names a customer', () => {
    auditClient.timeline
      .mockReturnValueOnce(of(page(adminRows(100), 150)))
      .mockReturnValueOnce(of(page(adminRows(50), 150)));

    facade.exportIncidentFile();

    expect(auditClient.timeline).toHaveBeenCalledTimes(2);
    expect(gdprClient.incidentFile).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'pages.order_detail.incident_file.no_subject'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('says so when the trail names no customer, and asks for no file', () => {
    auditClient.timeline.mockReturnValue(
      of(page([entry(TimelineSource.Customer, undefined, true), entry(TimelineSource.Employee, 'emp-1', true)]))
    );

    facade.exportIncidentFile();

    expect(gdprClient.incidentFile).not.toHaveBeenCalled();
    expect(download).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'pages.order_detail.incident_file.no_subject'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('surfaces a refusal from the build and downloads nothing', () => {
    const error = new Error('boom');
    gdprClient.incidentFile.mockReturnValue(throwError(() => error));

    facade.exportIncidentFile();

    expect(download).not.toHaveBeenCalled();
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(snackbar.showApiError).toHaveBeenCalledWith(
      error,
      'pages.order_detail.incident_file.error'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('surfaces a failed trail read the same way', () => {
    const error = new Error('boom');
    auditClient.timeline.mockReturnValue(throwError(() => error));

    facade.exportIncidentFile();

    expect(gdprClient.incidentFile).not.toHaveBeenCalled();
    expect(snackbar.showApiError).toHaveBeenCalledWith(
      error,
      'pages.order_detail.incident_file.error'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('does nothing without a loaded order, and does not fire twice while a build is in flight', () => {
    facade.order.set(null);
    facade.exportIncidentFile();
    expect(auditClient.timeline).not.toHaveBeenCalled();

    facade.order.set(OrderItem.fromJS({ id: 'order-1' }));
    facade.incidentFileExporting.set(true);
    facade.exportIncidentFile();
    expect(auditClient.timeline).not.toHaveBeenCalled();
  });
});

describe('OrderDetailFacade — customer account', () => {
  let facade: OrderDetailFacade;
  let details: jest.Mock;
  let customer: jest.Mock;
  let navigate: jest.Mock;
  let hasPolicy: jest.Mock;

  beforeEach(() => {
    details = jest.fn().mockImplementation((id: string) => of(OrderItem.fromJS({ id })));
    customer = jest.fn().mockReturnValue(of(null));
    navigate = jest.fn();
    hasPolicy = jest.fn().mockReturnValue(true);
    TestBed.configureTestingModule({ providers: [
      OrderDetailFacade,
      { provide: AdminClient, useValue: { adminOrderClient: { details, customer } } },
      { provide: AdminGdprClient, useValue: {} },
      { provide: CustomerAuditClient, useValue: { timeline: jest.fn() } },
      { provide: SnackbarService, useValue: { showApiError: jest.fn() } },
      { provide: TranslateService, useValue: { instant: (key: string) => key } },
      { provide: Router, useValue: { navigate } },
      { provide: PermissionService, useValue: { hasPolicy } },
    ] });
    facade = TestBed.inject(OrderDetailFacade);
  });

  it('reads only the order-keyed endpoint and permits navigation only for a same-company customer', () => {
    customer.mockReturnValue(of(UserItem.fromJS({ id: 'same' })));
    facade.loadOrderDetail('order-1');
    expect(customer).toHaveBeenCalledWith('order-1');
    facade.openCustomer();
    expect(navigate).toHaveBeenCalledWith(['customers', 'same']);
    customer.mockReturnValue(of(UserItem.fromJS({ id: 'foreign', customerOfAnotherCompany: { id: 'foreign', firstName: 'First', maskedEmail: 'f***@mail.test', companyName: 'Cleansia CZ' } })));
    facade.loadOrderDetail('order-2');
    expect(facade.customerId()).toBeUndefined();
    facade.openCustomer();
    expect(navigate).toHaveBeenCalledTimes(1);
    expect(TestBed.inject(CustomerAuditClient).timeline).not.toHaveBeenCalled();
  });

  it('clears and cancels old customer requests before a different order can be exposed', () => {
    const oldCustomer = new Subject<UserItem>();
    const newCustomer = new Subject<UserItem>();
    customer.mockReturnValueOnce(oldCustomer).mockReturnValueOnce(newCustomer);
    facade.loadOrderDetail('old');
    expect(facade.customerLoading()).toBe(true);
    facade.loadOrderDetail('new');
    expect(oldCustomer.observed).toBe(false);
    expect(facade.customer()).toBeNull();
    oldCustomer.next(UserItem.fromJS({ id: 'old-customer' }));
    expect(facade.customer()).toBeNull();
    newCustomer.next(UserItem.fromJS({ id: 'new-customer' }));
    newCustomer.complete();
    expect(facade.customerId()).toBe('new-customer');
    expect(facade.customerLoading()).toBe(false);
  });

  it('cancels old detail responses and clears prior customer data while reloading', () => {
    customer.mockReturnValue(of(UserItem.fromJS({ id: 'first' })));
    facade.loadOrderDetail('first');
    const pending = new Subject<OrderItem>();
    details.mockReturnValueOnce(pending);
    facade.loadOrderDetail('old');
    expect(facade.customer()).toBeNull();
    facade.loadOrderDetail('new');
    expect(pending.observed).toBe(false);
    expect(customer).not.toHaveBeenCalledWith('old');
    expect(facade.order()?.id).toBe('new');
  });

  it('distinguishes empty accounts from failures and preserves the order on account failure', () => {
    facade.loadOrderDetail('guest');
    expect(facade.customer()).toBeNull();
    expect(facade.customerError()).toBe(false);
    customer.mockReturnValue(throwError(() => new Error('offline')));
    facade.loadOrderDetail('failed');
    expect(facade.customerError()).toBe(true);
    expect(facade.customerLoading()).toBe(false);
    expect(facade.order()?.id).toBe('failed');
    customer.mockReturnValue(of(null));
    facade.loadOrderDetail('retry');
    expect(facade.customerError()).toBe(false);
  });

  it('treats a guest account ProblemDetails response as empty without a toast', () => {
    details.mockReturnValue(of(OrderItem.fromJS({ id: 'guest', customerName: 'Guest', customerEmail: 'guest@example.test' })));
    customer.mockReturnValue(throwError(() => ({
      status: 400, title: 'Bad Request', detail: 'A validation problem occurred.',
      errors: { OrderNotFound: 'order.not_found' },
    })));
    facade.loadOrderDetail('guest');
    expect(facade.customer()).toBeNull();
    expect(facade.customerError()).toBe(false);
    expect(facade.order()?.customerEmail).toBe('guest@example.test');
    expect(TestBed.inject(SnackbarService).showApiError).not.toHaveBeenCalled();
  });

  it('does not request accounts without the admin-only customer policy', () => {
    expect(resolvePhysicalPolicy(Policy.CanViewOrderCustomer)).toBe(PhysicalPolicy.AdminOnly);
    hasPolicy.mockReturnValue(false);
    facade.loadOrderDetail('order');
    expect(customer).not.toHaveBeenCalled();
    expect(facade.customerLoading()).toBe(false);
  });
});
