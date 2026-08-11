import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminReferralListItem,
  GetReferralsByUserResponse,
  GrantPointsManuallyCommand,
  ReferralStatus,
  RevokePointsManuallyCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
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
