import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminReferralListItem,
  ForceQualifyReferralResponse,
  PagedDataOfAdminReferralListItem,
  ReferralStatus,
  ReverseReferralResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ReferralsListFacade } from './referrals-list.facade';

describe('ReferralsListFacade', () => {
  let facade: ReferralsListFacade;
  let referralClient: {
    getPaged: jest.Mock;
    reverse: jest.Mock;
    forceQualify: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const page = PagedDataOfAdminReferralListItem.fromJS({
    data: [
      AdminReferralListItem.fromJS({
        id: 'ref-1',
        referrerEmail: 'a@x.cz',
        referredEmail: 'b@x.cz',
        status: ReferralStatus.Qualified,
      }),
    ],
    total: 1,
  });

  beforeEach(() => {
    referralClient = {
      getPaged: jest.fn(),
      reverse: jest.fn(),
      forceQualify: jest.fn(),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ReferralsListFacade,
        {
          provide: AdminClient,
          useValue: { adminReferralClient: referralClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(ReferralsListFacade);
  });

  it('loads referrals and stores data + total', () => {
    referralClient.getPaged.mockReturnValue(of(page));

    facade.loadReferrals();

    expect(referralClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.referrals().length).toBe(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  it('maps the reversed UI filter onto ReferralStatus.Reversed', () => {
    referralClient.getPaged.mockReturnValue(of(page));

    facade.applyFilter({ status: 'reversed' });

    const args = referralClient.getPaged.mock.calls[0];
    expect(args[0]).toBe(ReferralStatus.Reversed);
  });

  it('reverses a referral with the trimmed reason and reloads on success', () => {
    referralClient.reverse.mockReturnValue(
      of(
        ReverseReferralResponse.fromJS({
          referralId: 'ref-1',
          pointsRevokedFromReferrer: 100,
          pointsRevokedFromReferred: 50,
        })
      )
    );
    referralClient.getPaged.mockReturnValue(of(page));
    const onSuccess = jest.fn();

    facade.reverseReferral('ref-1', '  fraud ring  ', onSuccess);

    expect(referralClient.reverse).toHaveBeenCalledTimes(1);
    const [id, command] = referralClient.reverse.mock.calls[0];
    expect(id).toBe('ref-1');
    expect(command.referralId).toBe('ref-1');
    expect(command.reason).toBe('fraud ring');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_reverse'
    );
    expect(referralClient.getPaged).toHaveBeenCalledTimes(1);
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.intervening()).toBe(false);
  });

  it('does not call reverse with a blank reason', () => {
    facade.reverseReferral('ref-1', '   ', jest.fn());
    expect(referralClient.reverse).not.toHaveBeenCalled();
  });

  it('maps referral.not_qualified on reverse failure', () => {
    referralClient.reverse.mockReturnValue(
      throwError(() => ({ result: { detail: 'referral.not_qualified' } }))
    );

    facade.reverseReferral('ref-1', 'reason', jest.fn());

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.referral.not_qualified'
    );
    expect(facade.intervening()).toBe(false);
  });

  it('force-qualifies a referral and reloads on success', () => {
    referralClient.forceQualify.mockReturnValue(
      of(
        ForceQualifyReferralResponse.fromJS({
          referralId: 'ref-2',
          pointsGrantedToReferrer: 100,
          pointsGrantedToReferred: 50,
        })
      )
    );
    referralClient.getPaged.mockReturnValue(of(page));
    const onSuccess = jest.fn();

    facade.forceQualifyReferral('ref-2', 'legit order confirmed', onSuccess);

    const [id, command] = referralClient.forceQualify.mock.calls[0];
    expect(id).toBe('ref-2');
    expect(command.reason).toBe('legit order confirmed');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_force_qualify'
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });

  it('maps referral.not_accepted on force-qualify failure', () => {
    referralClient.forceQualify.mockReturnValue(
      throwError(() => ({ result: { detail: 'referral.not_accepted' } }))
    );

    facade.forceQualifyReferral('ref-2', 'reason', jest.fn());

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.referral.not_accepted'
    );
  });

  it('maps referral.reason_required on intervention failure', () => {
    referralClient.reverse.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'referral.reason_required' }),
      }))
    );

    facade.reverseReferral('ref-1', 'reason', jest.fn());

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.referral.reason_required'
    );
  });

  it('falls back to the generic referral error for unknown codes', () => {
    referralClient.reverse.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.reverseReferral('ref-1', 'reason', jest.fn());

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.referral.action_failed'
    );
  });

  it('ignores a second intervention while one is in flight', () => {
    facade.intervening.set(true);

    facade.reverseReferral('ref-1', 'reason', jest.fn());
    facade.forceQualifyReferral('ref-2', 'reason', jest.fn());

    expect(referralClient.reverse).not.toHaveBeenCalled();
    expect(referralClient.forceQualify).not.toHaveBeenCalled();
  });
});
