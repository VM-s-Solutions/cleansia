/* Dialog stubs mirror the real selectors and bindings so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/component-class-suffix */
import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminClient,
  GetReferralsByUserResponse,
  GetUserCreditResponse,
  GetUserLoyaltyAccountResponse,
  LoyaltyTier,
} from '@cleansia/admin-services';
import { PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { ExpireCreditDialogComponent } from '../expire-credit-dialog/expire-credit-dialog.component';
import { GrantPointsDialogComponent } from '../grant-points-dialog/grant-points-dialog.component';
import { IssueCreditDialogComponent } from '../issue-credit-dialog/issue-credit-dialog.component';
import { UserLoyaltyDetailComponent } from './user-loyalty-detail.component';

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
  currencyCode = input('');
  visibleChange = output<boolean>();
  submitForm = output<string>();
}

describe('UserLoyaltyDetailComponent — credit section', () => {
  let creditClient: { user: jest.Mock; issue: jest.Mock; expire: jest.Mock };

  beforeEach(async () => {
    creditClient = { user: jest.fn(), issue: jest.fn(), expire: jest.fn() };

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
          },
        },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn(), showError: jest.fn() } },
        { provide: PermissionService, useValue: { hasPolicy: () => true } },
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
          imports: [GrantPointsDialogComponent, IssueCreditDialogComponent, ExpireCreditDialogComponent],
        },
        add: {
          imports: [GrantPointsDialogStub, IssueCreditDialogStub, ExpireCreditDialogStub],
        },
      })
      .compileComponents();
  });

  function render(credit: GetUserCreditResponse): HTMLElement {
    creditClient.user.mockReturnValue(of(credit));
    const fixture: ComponentFixture<UserLoyaltyDetailComponent> =
      TestBed.createComponent(UserLoyaltyDetailComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

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
});
