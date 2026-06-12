import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminReferralListItem,
  GetReferralsByUserResponse,
  ReferralStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { UserLoyaltyDetailFacade } from './user-loyalty-detail.facade';

describe('UserLoyaltyDetailFacade — referrals panel', () => {
  let facade: UserLoyaltyDetailFacade;
  let referralClient: { byUser: jest.Mock };

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

    TestBed.configureTestingModule({
      providers: [
        UserLoyaltyDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminLoyaltyClient: {},
            adminReferralClient: referralClient,
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
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
});
