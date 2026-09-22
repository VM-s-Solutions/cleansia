import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminCurrencyListItem,
  AdminGdprClient,
  AdminReferralListItem,
  CreditTransactionReason,
  ExpireCustomerCreditCommand,
  ExpireCustomerCreditResponse,
  FileResponse,
  GdprExportDto,
  GetReferralsByUserResponse,
  GrantPointsManuallyCommand,
  IssueCustomerCreditCommand,
  IssueCustomerCreditResponse,
  OrderListItem,
  PagedDataOfOrderListItem,
  ReferralStatus,
  RevokePointsManuallyCommand,
  SortDirection,
} from '@cleansia/admin-services';
import { FileDownloadService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { UserLoyaltyDetailFacade } from './user-loyalty-detail.facade';

describe('UserLoyaltyDetailFacade — referrals panel', () => {
  let facade: UserLoyaltyDetailFacade;
  let referralClient: { byUser: jest.Mock };
  let loyaltyClient: {
    userAccount: jest.Mock;
    userActivity: jest.Mock;
    grantPoints: jest.Mock;
    revokePoints: jest.Mock;
  };

  const byUserResponse = GetReferralsByUserResponse.fromJS({
    asReferrer: [
      AdminReferralListItem.fromJS({
        id: 'ref-1',
        referredEmail: 'friend@x.cz',
        status: ReferralStatus.Qualified,
      }),
    ],
    asReferred: [
      AdminReferralListItem.fromJS({
        id: 'ref-2',
        referrerEmail: 'inviter@x.cz',
        status: ReferralStatus.Accepted,
      }),
    ],
  });

  beforeEach(() => {
    referralClient = { byUser: jest.fn() };
    loyaltyClient = {
      userAccount: jest.fn().mockReturnValue(of(null)),
      userActivity: jest.fn().mockReturnValue(of(null)),
      grantPoints: jest.fn().mockReturnValue(of({})),
      revokePoints: jest.fn().mockReturnValue(of({})),
    };

    TestBed.configureTestingModule({
      providers: [
        UserLoyaltyDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminLoyaltyClient: loyaltyClient,
            adminReferralClient: referralClient,
          },
        },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AdminGdprClient, useValue: { export: jest.fn() } },
      ],
    });

    facade = TestBed.inject(UserLoyaltyDetailFacade);
  });

  it('loads the by-user referral relationships into both lists', () => {
    referralClient.byUser.mockReturnValue(of(byUserResponse));

    facade.loadReferrals('user-1');

    expect(referralClient.byUser).toHaveBeenCalledWith('user-1');
    expect(facade.referralsAsReferrer().length).toBe(1);
    expect(facade.referralsAsReferrer()[0].id).toBe('ref-1');
    expect(facade.referralsAsReferred().length).toBe(1);
    expect(facade.referralsAsReferred()[0].id).toBe('ref-2');
    expect(facade.referralsLoading()).toBe(false);
    expect(facade.referralsError()).toBe(false);
  });

  it('handles empty referral lists', () => {
    referralClient.byUser.mockReturnValue(
      of(GetReferralsByUserResponse.fromJS({}))
    );

    facade.loadReferrals('user-1');

    expect(facade.referralsAsReferrer().length).toBe(0);
    expect(facade.referralsAsReferred().length).toBe(0);
  });

  it('sets the error flag and clears loading on failure', () => {
    referralClient.byUser.mockReturnValue(throwError(() => new Error('x')));

    facade.loadReferrals('user-1');

    expect(facade.referralsError()).toBe(true);
    expect(facade.referralsLoading()).toBe(false);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    // jsdom's crypto carries no randomUUID; the counter also lets the per-attempt
    // property be asserted rather than assumed.
    let issued: number;
    let originalRandomUUID: Crypto['randomUUID'] | undefined;

    beforeEach(() => {
      issued = 0;
      originalRandomUUID = crypto.randomUUID;
      Object.defineProperty(crypto, 'randomUUID', {
        configurable: true,
        value: () => `request-${++issued}`,
      });
      facade.loadAccount('user-1');
    });

    afterEach(() => {
      Object.defineProperty(crypto, 'randomUUID', {
        configurable: true,
        value: originalRandomUUID,
      });
    });

    it('serializes a grant with the user, the points, the reason and an idempotency key', () => {
      facade.grantPoints({ points: 250, reason: 'Goodwill after a late crew' });

      const command: GrantPointsManuallyCommand =
        loyaltyClient.grantPoints.mock.calls[0][0];
      expect(command).toBeInstanceOf(GrantPointsManuallyCommand);
      expect(command.toJSON()).toEqual({
        userId: 'user-1',
        points: 250,
        reason: 'Goodwill after a late crew',
        // Losing this collapses retry protection: the server can no longer
        // recognise a repeated grant as the same one.
        requestId: 'request-1',
      });
    });

    it('serializes a revoke with the user, the points, the reason and an idempotency key', () => {
      facade.revokePoints({ points: 100, reason: 'Duplicate grant' });

      const command: RevokePointsManuallyCommand =
        loyaltyClient.revokePoints.mock.calls[0][0];
      expect(command).toBeInstanceOf(RevokePointsManuallyCommand);
      expect(command.toJSON()).toEqual({
        userId: 'user-1',
        points: 100,
        reason: 'Duplicate grant',
        requestId: 'request-1',
      });
    });

    it('mints a fresh idempotency key per submission, so two clicks are two grants', () => {
      facade.grantPoints({ points: 10, reason: 'first' });
      facade.grantPoints({ points: 10, reason: 'second' });

      const [first] = loyaltyClient.grantPoints.mock.calls[0];
      const [second] = loyaltyClient.grantPoints.mock.calls[1];
      expect(first.requestId).not.toBe(second.requestId);
    });
  });
});

describe('UserLoyaltyDetailFacade — credit', () => {
  let facade: UserLoyaltyDetailFacade;
  let creditClient: { user: jest.Mock; issue: jest.Mock; expire: jest.Mock };
  let currencyClient: { getOverview: jest.Mock };
  let originalRandomUUID: Crypto['randomUUID'] | undefined;

  beforeEach(() => {
    creditClient = {
      user: jest.fn().mockReturnValue(of(null)),
      issue: jest
        .fn()
        .mockReturnValue(of(IssueCustomerCreditResponse.fromJS({ newBalance: 50 }))),
      expire: jest.fn().mockReturnValue(of(null)),
    };
    currencyClient = { getOverview: jest.fn().mockReturnValue(of([])) };

    TestBed.configureTestingModule({
      providers: [
        UserLoyaltyDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCreditClient: creditClient,
            adminCurrencyClient: currencyClient,
          },
        },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AdminGdprClient, useValue: { export: jest.fn() } },
      ],
    });
    facade = TestBed.inject(UserLoyaltyDetailFacade);

    originalRandomUUID = crypto.randomUUID;
    Object.defineProperty(crypto, 'randomUUID', {
      configurable: true,
      value: () => 'request-1',
    });
    // Sets currentUserId; the mocked read answers null so nothing else happens.
    facade.loadCredit('user-1');
  });

  afterEach(() => {
    Object.defineProperty(crypto, 'randomUUID', {
      configurable: true,
      value: originalRandomUUID,
    });
  });

  it('offers only the currencies the platform operates in', () => {
    currencyClient.getOverview.mockReturnValue(
      of([
        AdminCurrencyListItem.fromJS({ id: 'cur-czk', code: 'CZK', isDefault: true, isActive: true }),
        AdminCurrencyListItem.fromJS({ id: 'cur-eur', code: 'EUR', isDefault: false, isActive: true }),
        AdminCurrencyListItem.fromJS({ id: 'cur-usd', code: 'USD', isDefault: false, isActive: false }),
      ])
    );

    facade.loadCurrencies();

    expect(facade.currencies()).toEqual([
      { id: 'cur-czk', code: 'CZK' },
      { id: 'cur-eur', code: 'EUR' },
    ]);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks. This pins
  // the serialized body instead (ADR-0031).
  it('serializes a grant with the currency the admin chose beside the amount', () => {
    facade.issueCredit({
      amount: 50,
      currencyId: 'cur-eur',
      reason: CreditTransactionReason.Goodwill,
      note: 'Late crew',
    });

    const command: IssueCustomerCreditCommand = creditClient.issue.mock.calls[0][0];
    expect(command).toBeInstanceOf(IssueCustomerCreditCommand);
    expect(command.toJSON()).toEqual({
      userId: 'user-1',
      amount: 50,
      // Losing this puts the money in whatever the server picks — the defect this closes.
      currencyId: 'cur-eur',
      reason: CreditTransactionReason.Goodwill,
      note: 'Late crew',
      requestId: 'request-1',
    });
  });

  // The discharge names ONE account. A customer holding CZK and EUR credit has two, and the server
  // drains only the currency it is told — dropping this assignment is a 400 (required), not a guess.
  it('serializes a discharge with the currency of the account the admin chose', () => {
    creditClient.expire.mockReturnValue(
      of(ExpireCustomerCreditResponse.fromJS({ userId: 'user-1', amountExpired: 50, currencyCode: 'EUR' }))
    );

    facade.expireCredit({ currencyId: 'cur-eur', note: 'Leaving' });

    const command: ExpireCustomerCreditCommand = creditClient.expire.mock.calls[0][0];
    expect(command).toBeInstanceOf(ExpireCustomerCreditCommand);
    expect(command.toJSON()).toEqual({
      userId: 'user-1',
      currencyId: 'cur-eur',
      note: 'Leaving',
      requestId: 'request-1',
    });
  });

  it('confirms the discharge in the currency the server drained', () => {
    const snackbar = TestBed.inject(SnackbarService);
    creditClient.expire.mockReturnValue(
      of(ExpireCustomerCreditResponse.fromJS({ userId: 'user-1', amountExpired: 50, currencyCode: 'EUR' }))
    );

    facade.expireCredit({ currencyId: 'cur-eur', note: 'Leaving' });

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.loyalty_user_detail.credit.expire_success', {
      amount: 50,
      currency: 'EUR',
    });
    expect(creditClient.user).toHaveBeenCalledTimes(2);
  });
});

describe('UserLoyaltyDetailFacade — credit money', () => {
  let facade: UserLoyaltyDetailFacade;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        UserLoyaltyDetailFacade,
        { provide: AdminClient, useValue: {} },
        { provide: SnackbarService, useValue: {} },
        { provide: TranslateService, useValue: { instant: (k: string) => k, currentLang: 'cs' } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AdminGdprClient, useValue: {} },
      ],
    });
    facade = TestBed.inject(UserLoyaltyDetailFacade);
  });

  it('writes a balance as money to two places in the session language, as every admin detail does', () => {
    expect(facade.formatBalance(1250, 'CZK')).toBe('1 250,00 Kč');
    expect(facade.formatBalance(0, 'CZK')).toBe('0,00 Kč');
  });

  it('writes a ledger amount signed with a space, the way the invoice ledger does, so a spend reads as a spend', () => {
    expect(facade.formatLedgerAmount(1250, 'CZK')).toBe('+ 1 250,00 Kč');
    expect(facade.formatLedgerAmount(-300.5, 'CZK')).toBe('- 300,50 Kč');
    expect(facade.formatLedgerAmount(undefined, 'CZK')).toBe('0,00 Kč');
  });
});

describe('UserLoyaltyDetailFacade — subject export', () => {
  let facade: UserLoyaltyDetailFacade;
  let gdprClient: { export: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showApiError: jest.Mock;
  };
  let download: jest.SpyInstance;

  const exportDto = GdprExportDto.fromJS({
    exportedAt: '2026-09-13T10:00:00Z',
    customerActions: [
      {
        action: 'customer.order.cancel',
        occurredOn: '2026-09-13T09:00:00Z',
        resourceType: 'Order',
        resourceId: 'order-1',
        success: true,
      },
    ],
  });

  beforeEach(() => {
    gdprClient = { export: jest.fn().mockReturnValue(of(exportDto)) };
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showApiError: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        UserLoyaltyDetailFacade,
        {
          provide: AdminClient,
          useValue: { adminCreditClient: { user: jest.fn().mockReturnValue(of(null)) } },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AdminGdprClient, useValue: gdprClient },
      ],
    });
    facade = TestBed.inject(UserLoyaltyDetailFacade);
    download = jest
      .spyOn(
        facade as unknown as { downloadJson: (d: unknown, n: string) => void },
        'downloadJson'
      )
      .mockImplementation(() => undefined);
  });

  it('calls the admin export for the loaded user and downloads it as a json named by user and date', () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-09-13T22:30:00Z'));
    facade.loadCredit('user-1');

    facade.exportSubjectData();

    expect(gdprClient.export).toHaveBeenCalledWith('user-1');
    expect(download).toHaveBeenCalledWith(exportDto, 'subject-export-user-1-2026-09-13.json');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.customer_detail.export_success');
    expect(facade.exporting()).toBe(false);
    jest.useRealTimers();
  });

  // The file is JSON.stringify(dto), which runs the generated toJSON — a client generated before the
  // export DTO gained customerActions drops the whole trail from the download while every mock stays
  // green, so this pins the wire shape, not the mock.
  it('writes the customer trail into the downloaded file', () => {
    facade.loadCredit('user-1');

    facade.exportSubjectData();

    const written = JSON.parse(JSON.stringify(download.mock.calls[0][0]));
    expect(written.customerActions).toBeDefined();
    expect(written.customerActions[0].action).toBe('customer.order.cancel');
  });

  it('surfaces the API error and downloads nothing on failure', () => {
    const error = new Error('boom');
    gdprClient.export.mockReturnValue(throwError(() => error));
    facade.loadCredit('user-1');

    facade.exportSubjectData();

    expect(download).not.toHaveBeenCalled();
    expect(snackbar.showApiError).toHaveBeenCalledWith(error, 'pages.customer_detail.export_error');
    expect(facade.exporting()).toBe(false);
  });

  it('does nothing without a loaded user, and does not fire twice while an export is in flight', () => {
    facade.exportSubjectData();
    expect(gdprClient.export).not.toHaveBeenCalled();

    facade.loadCredit('user-1');
    facade.exporting.set(true);
    facade.exportSubjectData();
    expect(gdprClient.export).not.toHaveBeenCalled();
  });
});

describe('UserLoyaltyDetailFacade — incident file', () => {
  let facade: UserLoyaltyDetailFacade;
  let gdprClient: { export: jest.Mock; incidentFile: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showApiError: jest.Mock;
  };
  let download: jest.Mock;

  const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
  const served: FileResponse = {
    data: pdf,
    status: 200,
    fileName: 'incident-user-1-20260914.pdf',
  };

  beforeEach(() => {
    gdprClient = {
      export: jest.fn(),
      incidentFile: jest.fn().mockReturnValue(of(served)),
    };
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showApiError: jest.fn(),
    };
    download = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        UserLoyaltyDetailFacade,
        {
          provide: AdminClient,
          useValue: { adminCreditClient: { user: jest.fn().mockReturnValue(of(null)) } },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AdminGdprClient, useValue: gdprClient },
        { provide: FileDownloadService, useValue: { downloadBlob: download } },
      ],
    });
    facade = TestBed.inject(UserLoyaltyDetailFacade);
  });

  it('asks for the whole account when no order scope is given and saves the served file under its own name', () => {
    facade.loadCredit('user-1');

    facade.exportIncidentFile('');

    expect(gdprClient.incidentFile).toHaveBeenCalledWith('user-1', undefined);
    expect(download).toHaveBeenCalledWith(pdf, 'incident-user-1-20260914.pdf');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.customer_detail.incident_file.success'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('scopes the file to the order the admin picked', () => {
    facade.loadCredit('user-1');

    facade.exportIncidentFile('order-7');

    expect(gdprClient.incidentFile).toHaveBeenCalledWith('user-1', 'order-7');
  });

  // Content-Disposition is exposed by the API's CORS policy, so the served name normally wins; the
  // fallback is the same shape the server prints, on the UTC day, so a file never carries two names.
  it('names the file by user and UTC day when the server sends no file name', () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-09-14T23:30:00Z'));
    gdprClient.incidentFile.mockReturnValue(of({ data: pdf, status: 200 }));
    facade.loadCredit('user-1');

    facade.exportIncidentFile(null);

    expect(download).toHaveBeenCalledWith(pdf, 'incident-user-1-20260914.pdf');
    jest.useRealTimers();
  });

  it('surfaces the refusal and downloads nothing on failure', () => {
    const error = new Error('boom');
    gdprClient.incidentFile.mockReturnValue(throwError(() => error));
    facade.loadCredit('user-1');

    facade.exportIncidentFile('order-7');

    expect(download).not.toHaveBeenCalled();
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(snackbar.showApiError).toHaveBeenCalledWith(
      error,
      'pages.customer_detail.incident_file.error'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('does nothing without a loaded user, and does not fire twice while a build is in flight', () => {
    facade.exportIncidentFile('order-7');
    expect(gdprClient.incidentFile).not.toHaveBeenCalled();

    facade.loadCredit('user-1');
    facade.incidentFileExporting.set(true);
    facade.exportIncidentFile('order-7');
    expect(gdprClient.incidentFile).not.toHaveBeenCalled();
  });

  it('opens the scope panel on the toggle and closes it once the file is built', () => {
    expect(facade.incidentPanelOpen()).toBe(false);

    facade.toggleIncidentPanel();
    expect(facade.incidentPanelOpen()).toBe(true);
    facade.toggleIncidentPanel();
    expect(facade.incidentPanelOpen()).toBe(false);

    facade.toggleIncidentPanel();
    facade.loadCredit('user-1');
    facade.exportIncidentFile('order-7');
    expect(facade.incidentPanelOpen()).toBe(false);
  });

  it('leaves the scope panel open when the build is refused', () => {
    gdprClient.incidentFile.mockReturnValue(throwError(() => new Error('boom')));
    facade.toggleIncidentPanel();
    facade.loadCredit('user-1');

    facade.exportIncidentFile('order-7');

    expect(facade.incidentPanelOpen()).toBe(true);
  });
});

describe('UserLoyaltyDetailFacade — incident order picker', () => {
  let facade: UserLoyaltyDetailFacade;
  let getPaged: jest.Mock;

  const USER_ID_SLOT = 18;
  const SORT_SLOT = 19;
  const OFFSET_SLOT = 20;
  const LIMIT_SLOT = 21;

  const page = (orders: Partial<OrderListItem>[], total = orders.length) =>
    PagedDataOfOrderListItem.fromJS({ data: orders, total });

  beforeEach(() => {
    getPaged = jest.fn().mockReturnValue(of(page([])));

    TestBed.configureTestingModule({
      providers: [
        UserLoyaltyDetailFacade,
        { provide: AdminClient, useValue: { adminOrderClient: { getPaged } } },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
            showApiError: jest.fn(),
          },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: AdminGdprClient, useValue: { export: jest.fn(), incidentFile: jest.fn() } },
        { provide: FileDownloadService, useValue: { downloadBlob: jest.fn() } },
      ],
    });
    facade = TestBed.inject(UserLoyaltyDetailFacade);
  });

  // The account is the only filter: a cancelled or completed booking is as much an incident's
  // subject as a live one, and the newest cleaning is the one an admin is most often asked about.
  it('asks for the orders of the one account, newest cleaning first, in one page', () => {
    facade.loadSubjectOrders('user-1');

    const call = getPaged.mock.lastCall as unknown[];
    expect(call[USER_ID_SLOT]).toBe('user-1');
    expect(call[SORT_SLOT]).toEqual([
      expect.objectContaining({ field: 'cleaningDateTime', direction: SortDirection.Descending }),
    ]);
    expect(call[OFFSET_SLOT]).toBe(0);
    expect(call[LIMIT_SLOT]).toBe(100);
    call.forEach((arg, slot) => {
      if (![USER_ID_SLOT, SORT_SLOT, OFFSET_SLOT, LIMIT_SLOT].includes(slot)) {
        expect(arg).toBeUndefined();
      }
    });
  });

  it('offers each order by its display number and cleaning date, valued by its id', () => {
    getPaged.mockReturnValue(
      of(
        page([
          { id: 'order-7', displayOrderNumber: 'ORD-7', cleaningDateTime: new Date('2026-09-14T10:00:00Z') },
          { id: 'order-3', displayOrderNumber: 'ORD-3', cleaningDateTime: new Date('2026-03-02T08:30:00Z') },
          { displayOrderNumber: 'no-id' },
        ])
      )
    );

    facade.loadSubjectOrders('user-1');

    const options = facade.incidentOrderOptions();
    expect(options.map((o) => o.value)).toEqual(['order-7', 'order-3']);
    expect(options[0].label).toMatch(/^ORD-7 · .*2026/);
    expect(options[1].label).toMatch(/^ORD-3 · .*2026/);
    expect(facade.subjectOrdersLoading()).toBe(false);
    expect(facade.subjectOrdersError()).toBe(false);
    expect(facade.subjectOrdersTruncated()).toBe(false);
  });

  it('falls back to the id when an order has no display number', () => {
    getPaged.mockReturnValue(of(page([{ id: 'order-9' }])));

    facade.loadSubjectOrders('user-1');

    expect(facade.incidentOrderOptions()).toEqual([{ label: 'order-9', value: 'order-9' }]);
  });

  it('reports an empty account as no options and no error', () => {
    facade.loadSubjectOrders('user-1');

    expect(facade.incidentOrderOptions()).toEqual([]);
    expect(facade.subjectOrdersError()).toBe(false);
    expect(facade.subjectOrdersLoading()).toBe(false);
  });

  it('flags a failed read as an error state rather than an empty account', () => {
    getPaged.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadSubjectOrders('user-1');

    expect(facade.subjectOrdersError()).toBe(true);
    expect(facade.subjectOrdersLoading()).toBe(false);
    expect(facade.incidentOrderOptions()).toEqual([]);
  });

  it('says when the account holds more orders than the page offers', () => {
    getPaged.mockReturnValue(of(page([{ id: 'order-1' }], 140)));

    facade.loadSubjectOrders('user-1');

    expect(facade.subjectOrdersTruncated()).toBe(true);
    expect(facade.subjectOrdersTotal()).toBe(140);
  });
});
