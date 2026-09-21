/* Dialog stubs mirror the real selectors and bindings so the override-imports swap is
   binding-compatible under the strict template test env. */
import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminClient,
  AdminGdprClient,
  GetReferralsByUserResponse,
  GetUserCreditCurrencyAccount,
  GetUserCreditResponse,
  GetUserLoyaltyAccountResponse,
  LoyaltyTier,
  PagedDataOfOrderListItem,
} from '@cleansia/admin-services';
import { TimelineComponent } from '@cleansia/admin-features/audit-log';
import { PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ExpireCreditDialogComponent } from '../expire-credit-dialog/expire-credit-dialog.component';
import { GrantPointsDialogComponent } from '../grant-points-dialog/grant-points-dialog.component';
import { IssueCreditDialogComponent } from '../issue-credit-dialog/issue-credit-dialog.component';
import { UserLoyaltyDetailComponent } from './user-loyalty-detail.component';
import { UserLoyaltyDetailFacade } from './user-loyalty-detail.facade';

@Component({ selector: 'cleansia-admin-grant-points-dialog', standalone: true, template: '' })
class GrantPointsDialogStub {
  visible = input(false);
  mode = input<string>('grant');
  submitting = input(false);
  visibleChange = output<boolean>();
  submitForm = output<unknown>();
}

@Component({ selector: 'cleansia-admin-issue-credit-dialog', standalone: true, template: '' })
class IssueCreditDialogStub {
  visible = input(false);
  submitting = input(false);
  currencies = input<unknown[]>([]);
  visibleChange = output<boolean>();
  submitForm = output<unknown>();
}

@Component({ selector: 'cleansia-admin-expire-credit-dialog', standalone: true, template: '' })
class ExpireCreditDialogStub {
  visible = input(false);
  submitting = input(false);
  balance = input(0);
  currencyId = input('');
  currencyCode = input('');
  visibleChange = output<boolean>();
  submitForm = output<unknown>();
}

@Component({ selector: 'cleansia-admin-audit-timeline', standalone: true, template: '' })
class TimelineStub {
  userId = input<string | null>(null);
  resourceType = input<string | null>(null);
  resourceId = input<string | null>(null);
  resourceLinks = input(true);
}

describe('UserLoyaltyDetailComponent — credit section', () => {
  let creditClient: { user: jest.Mock; issue: jest.Mock; expire: jest.Mock };
  let gdprClient: { export: jest.Mock; incidentFile: jest.Mock };
  let orderClient: { getPaged: jest.Mock };
  let grantedPolicies: Set<string>;

  const orderPage = (orders: { id: string; displayOrderNumber: string }[], total = orders.length) =>
    PagedDataOfOrderListItem.fromJS({ data: orders, total });

  beforeEach(async () => {
    creditClient = { user: jest.fn(), issue: jest.fn(), expire: jest.fn() };
    gdprClient = {
      export: jest.fn().mockReturnValue(of(null)),
      incidentFile: jest.fn().mockReturnValue(of(null)),
    };
    orderClient = {
      getPaged: jest.fn().mockReturnValue(of(orderPage([{ id: 'order-7', displayOrderNumber: 'ORD-7' }]))),
    };
    grantedPolicies = new Set<string>([
      'CanGrantLoyaltyPoints',
      'CanIssueCustomerCredit',
      'CanExpireCustomerCredit',
      'CanAdminExportUserData',
      'CanViewAuditLog',
    ]);

    await TestBed.configureTestingModule({
      imports: [UserLoyaltyDetailComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: AdminClient,
          useValue: {
            adminLoyaltyClient: {
              userAccount: jest.fn().mockReturnValue(
                of(
                  GetUserLoyaltyAccountResponse.fromJS({
                    userId: 'user-1',
                    currentTier: LoyaltyTier.BronzeCleaner,
                    lifetimePoints: 0,
                    completedBookingsCount: 0,
                  })
                )
              ),
              userActivity: jest.fn().mockReturnValue(of({ data: [], total: 0 })),
            },
            adminReferralClient: {
              byUser: jest.fn().mockReturnValue(of(GetReferralsByUserResponse.fromJS({}))),
            },
            adminCreditClient: creditClient,
            adminCurrencyClient: { getOverview: jest.fn().mockReturnValue(of([])) },
            adminOrderClient: orderClient,
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
        {
          provide: PermissionService,
          useValue: { hasPolicy: (p: string) => grantedPolicies.has(p) },
        },
        { provide: AdminGdprClient, useValue: gdprClient },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: (k: string) => (k === 'userId' ? 'user-1' : null) },
              queryParamMap: { get: () => null },
            },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideComponent(UserLoyaltyDetailComponent, {
        remove: {
          imports: [
            GrantPointsDialogComponent,
            IssueCreditDialogComponent,
            ExpireCreditDialogComponent,
            TimelineComponent,
          ],
        },
        add: {
          imports: [GrantPointsDialogStub, IssueCreditDialogStub, ExpireCreditDialogStub, TimelineStub],
        },
      })
      .compileComponents();
  });

  function renderFixture(credit: GetUserCreditResponse): ComponentFixture<UserLoyaltyDetailComponent> {
    creditClient.user.mockReturnValue(of(credit));
    const fixture: ComponentFixture<UserLoyaltyDetailComponent> =
      TestBed.createComponent(UserLoyaltyDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  function render(credit: GetUserCreditResponse): HTMLElement {
    return renderFixture(credit).nativeElement as HTMLElement;
  }

  // One discharge per FUNDED account, and the dialog is told which one: the server drains the
  // currency it is named and nothing else, so a single button over two balances would have to guess.
  it('offers a discharge beside each funded balance and hands the dialog that account', () => {
    const eurAccount = GetUserCreditCurrencyAccount.fromJS({
      accountId: 'acc-eur',
      balance: 25,
      currencyCode: 'EUR',
      currencyId: 'cur-eur',
      ledger: [],
    });
    const fixture = renderFixture(
      GetUserCreditResponse.fromJS({
        userId: 'user-1',
        hasAccount: true,
        balance: 400,
        currencyCode: 'CZK',
        ledger: [],
        accounts: [
          { accountId: 'acc-czk', balance: 400, currencyCode: 'CZK', currencyId: 'cur-czk', ledger: [] },
          eurAccount,
          { accountId: 'acc-pln', balance: 0, currencyCode: 'PLN', currencyId: 'cur-pln', ledger: [] },
        ],
      })
    );
    const el = fixture.nativeElement as HTMLElement;

    const blocks = el.querySelectorAll('.user-loyalty-detail__credit-account');
    expect(blocks.length).toBe(3);
    expect(blocks[0].querySelector('cleansia-button')).toBeTruthy();
    expect(blocks[1].querySelector('cleansia-button')).toBeTruthy();
    expect(blocks[2].querySelector('cleansia-button')).toBeNull();

    fixture.componentInstance.openExpireCredit(eurAccount);
    fixture.detectChanges();

    const dialog = fixture.debugElement.query(By.directive(ExpireCreditDialogStub))
      .componentInstance as ExpireCreditDialogStub;
    expect(dialog.currencyId()).toBe('cur-eur');
    expect(dialog.currencyCode()).toBe('EUR');
    expect(dialog.balance()).toBe(25);

    fixture.componentInstance.onExpireCreditDialogVisibleChange(false);
    expect(fixture.componentInstance.expireCreditAccount()).toBeNull();
  });

  it('renders one balance and one ledger per currency the customer holds', () => {
    const el = render(
      GetUserCreditResponse.fromJS({
        userId: 'user-1',
        hasAccount: true,
        balance: 400,
        currencyCode: 'CZK',
        ledger: [],
        accounts: [
          { accountId: 'acc-czk', balance: 400, currencyCode: 'CZK', ledger: [] },
          { accountId: 'acc-eur', balance: 25, currencyCode: 'EUR', ledger: [] },
        ],
      })
    );

    const blocks = el.querySelectorAll('.user-loyalty-detail__credit-account');
    expect(blocks.length).toBe(2);
    expect(blocks[0].querySelector('cleansia-table')).toBeTruthy();
    expect(blocks[1].querySelector('cleansia-table')).toBeTruthy();
    expect(el.textContent).toContain('400 CZK');
    expect(el.textContent).toContain('25 EUR');
  });

  it('says never credited, in the platform currency, for a customer with no account', () => {
    const el = render(
      GetUserCreditResponse.fromJS({
        userId: 'user-1',
        hasAccount: false,
        balance: 0,
        currencyCode: 'CZK',
        ledger: [],
        accounts: [],
      })
    );

    expect(el.querySelectorAll('.user-loyalty-detail__credit-account').length).toBe(0);
    expect(el.textContent).toContain('pages.loyalty_user_detail.credit.never_credited');
    expect(el.textContent).toContain('0 CZK');
  });

  const noAccount = () =>
    GetUserCreditResponse.fromJS({
      userId: 'user-1',
      hasAccount: false,
      balance: 0,
      currencyCode: 'CZK',
      ledger: [],
      accounts: [],
    });

  it('embeds the timeline keyed by the route user and offers the subject export to an admin who may', () => {
    const fixture = renderFixture(noAccount());

    const timeline = fixture.debugElement.query(By.directive(TimelineStub))
      .componentInstance as TimelineStub;
    expect(timeline.userId()).toBe('user-1');

    const exportButton = fixture.nativeElement.querySelector(
      '.cleansia-user-loyalty-detail__export'
    ) as HTMLElement | null;
    expect(exportButton).toBeTruthy();

    fixture.componentInstance.exportSubjectData();
    expect(gdprClient.export).toHaveBeenCalledWith('user-1');
  });

  it('hides the subject export without CanAdminExportUserData', () => {
    grantedPolicies.delete('CanAdminExportUserData');
    const el = render(noAccount());

    expect(el.querySelector('.cleansia-user-loyalty-detail__export')).toBeNull();
    expect(el.querySelector('cleansia-admin-audit-timeline')).toBeTruthy();
  });

  // The incident file is the same PII egress as the export, so it sits beside it under the same
  // policy, with the orders of the account offered as its scope and the picked id handed to the facade.
  it('offers the incident file with the orders of the account as its scope and hands the picked order to the facade', () => {
    const fixture = renderFixture(noAccount());
    const el = fixture.nativeElement as HTMLElement;
    const facade = fixture.debugElement.injector.get(UserLoyaltyDetailFacade);
    const exportIncidentFile = jest.spyOn(facade, 'exportIncidentFile');

    expect(orderClient.getPaged.mock.lastCall?.[18]).toBe('user-1');
    expect(el.querySelector('.cleansia-user-loyalty-detail__incident-file')).toBeTruthy();
    expect(
      el.querySelector('.cleansia-user-loyalty-detail__incident-scope cleansia-select')
    ).toBeTruthy();

    fixture.componentInstance.incidentOrderControl.setValue('order-7');
    fixture.componentInstance.exportIncidentFile();

    expect(exportIncidentFile).toHaveBeenCalledWith('order-7');
    expect(gdprClient.incidentFile).toHaveBeenCalledWith('user-1', 'order-7');
  });

  it('asks for the whole account when no order is picked', () => {
    const fixture = renderFixture(noAccount());

    fixture.componentInstance.exportIncidentFile();

    expect(gdprClient.incidentFile).toHaveBeenCalledWith('user-1', undefined);
  });

  it('says so instead of a picker when the account has no orders', () => {
    orderClient.getPaged.mockReturnValue(of(orderPage([])));
    const el = render(noAccount());

    const scope = el.querySelector('.cleansia-user-loyalty-detail__incident-scope');
    expect(scope?.querySelector('cleansia-select')).toBeNull();
    expect(scope?.textContent).toContain('pages.customer_detail.incident_file.no_orders');
  });

  it('offers a retry instead of a picker when the orders could not be read, and re-asks on it', () => {
    orderClient.getPaged.mockReturnValueOnce(throwError(() => new Error('boom')));
    const fixture = renderFixture(noAccount());
    const el = fixture.nativeElement as HTMLElement;

    const scope = el.querySelector('.cleansia-user-loyalty-detail__incident-scope');
    expect(scope?.querySelector('cleansia-select')).toBeNull();
    expect(scope?.textContent).toContain('pages.customer_detail.incident_file.orders_error');
    expect(scope?.querySelector('.cleansia-user-loyalty-detail__incident-scope-retry')).toBeTruthy();

    fixture.componentInstance.reloadSubjectOrders();
    fixture.detectChanges();

    expect(orderClient.getPaged).toHaveBeenCalledTimes(2);
    expect(
      el.querySelector('.cleansia-user-loyalty-detail__incident-scope cleansia-select')
    ).toBeTruthy();
  });

  it('names the page ceiling when the account holds more orders than it offers', () => {
    orderClient.getPaged.mockReturnValue(
      of(orderPage([{ id: 'order-7', displayOrderNumber: 'ORD-7' }], 140))
    );
    const el = render(noAccount());

    expect(el.querySelector('.cleansia-user-loyalty-detail__incident-scope')?.textContent).toContain(
      'pages.customer_detail.incident_file.orders_truncated'
    );
  });

  it('hides the incident file and its scope without CanAdminExportUserData', () => {
    grantedPolicies.delete('CanAdminExportUserData');
    const el = render(noAccount());

    expect(el.querySelector('.cleansia-user-loyalty-detail__incident-file')).toBeNull();
    expect(el.querySelector('.cleansia-user-loyalty-detail__incident-scope')).toBeNull();
  });

  // The timeline endpoint answers 403 without CanViewAuditLog; a section that always fails is worse
  // than none, and the sibling entry points (order and dispute History) gate on the same policy.
  it('hides the timeline section without CanViewAuditLog', () => {
    grantedPolicies.delete('CanViewAuditLog');
    const el = render(noAccount());

    expect(el.querySelector('cleansia-admin-audit-timeline')).toBeNull();
    expect(el.textContent).not.toContain('pages.customer_detail.timeline.title');
    expect(el.querySelector('.cleansia-user-loyalty-detail__export')).toBeTruthy();
  });
});
