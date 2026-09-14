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
  ReferralStatus,
  RevokePointsManuallyCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import {
  incidentFileName,
  UserLoyaltyDetailFacade,
} from './user-loyalty-detail.facade';

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
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
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
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
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
    const translate = TestBed.inject(TranslateService);
    const instant = jest.spyOn(translate, 'instant');
    creditClient.expire.mockReturnValue(
      of(ExpireCustomerCreditResponse.fromJS({ userId: 'user-1', amountExpired: 50, currencyCode: 'EUR' }))
    );

    facade.expireCredit({ currencyId: 'cur-eur', note: 'Leaving' });

    expect(instant).toHaveBeenCalledWith('pages.loyalty_user_detail.credit.expire_success', {
      amount: 50,
      currency: 'EUR',
    });
    expect(snackbar.showSuccess).toHaveBeenCalled();
    expect(creditClient.user).toHaveBeenCalledTimes(2);
  });
});

describe('UserLoyaltyDetailFacade — subject export', () => {
  let facade: UserLoyaltyDetailFacade;
  let gdprClient: { export: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock; showApiError: jest.Mock };
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
    snackbar = { showSuccess: jest.fn(), showError: jest.fn(), showApiError: jest.fn() };

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
    expect(snackbar.showSuccess).toHaveBeenCalledWith('pages.customer_detail.export_success');
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
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock; showApiError: jest.Mock };
  let download: jest.SpyInstance;

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
    snackbar = { showSuccess: jest.fn(), showError: jest.fn(), showApiError: jest.fn() };

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
        facade as unknown as { downloadBlob: (b: Blob, n: string) => void },
        'downloadBlob'
      )
      .mockImplementation(() => undefined);
  });

  it('asks for the whole account when no order scope is given and saves the served file under its own name', () => {
    facade.loadCredit('user-1');

    facade.exportIncidentFile('');

    expect(gdprClient.incidentFile).toHaveBeenCalledWith('user-1', undefined);
    expect(download).toHaveBeenCalledWith(pdf, 'incident-user-1-20260914.pdf');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.customer_detail.incident_file.success'
    );
    expect(facade.incidentFileExporting()).toBe(false);
  });

  it('scopes the file to the order the admin named', () => {
    facade.loadCredit('user-1');

    facade.exportIncidentFile('  order-7 ');

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
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
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
});

describe('incidentFileName', () => {
  it('prints the UTC day, the shape the server names the file by', () => {
    expect(incidentFileName('user-1', new Date('2026-09-14T23:30:00Z'))).toBe(
      'incident-user-1-20260914.pdf'
    );
  });
});
