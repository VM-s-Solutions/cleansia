import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminReferralListItem,
  ForceQualifyReferralCommand,
  ForceQualifyReferralResponse,
  PagedDataOfAdminReferralListItem,
  ReferralStatus,
  ReverseReferralCommand,
  ReverseReferralResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { ReferralsListFacade } from './referrals-list.facade';

describe('ReferralsListFacade', () => {
  let facade: ReferralsListFacade;
  let referralClient: {
    getPaged: jest.Mock;
    reverse: jest.Mock;
    forceQualify: jest.Mock;
  };
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

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
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        ReferralsListFacade,
        {
          provide: AdminClient,
          useValue: { adminReferralClient: referralClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'cs', onLangChange: EMPTY },
        },
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

  it('leaves the the reversed UI filter onto ReferralStatus.Reversed refusal to the interceptor toast', () => {
    referralClient.getPaged.mockReturnValue(of(page));

    facade.applyFilter({ status: 'reversed' });

    const args = referralClient.getPaged.mock.calls[0];
    expect(args[0]).toBe(ReferralStatus.Reversed);
  });

  describe('filters', () => {
    beforeEach(() => {
      jest.useFakeTimers();
      referralClient.getPaged.mockReturnValue(of(page));
    });

    afterEach(() => jest.useRealTimers());

    it('sends the chosen status once it settles and names it in one chip', () => {
      facade.filterForm.patchValue({ status: 'qualified' });
      jest.advanceTimersByTime(500);

      const args = referralClient.getPaged.mock.calls.at(-1);
      expect(args?.[0]).toBe(ReferralStatus.Qualified);
      expect(args?.[3]).toBe(0);
      expect(facade.filters.chips()).toEqual([
        {
          key: 'status',
          label: 'pages.loyalty_referrals.filter.status',
          value: 'pages.loyalty_referrals.filter.status_qualified',
        },
      ]);
    });

    it('sends the date range and shows it as one chip that clears both dates', () => {
      const from = new Date(2026, 8, 1);
      const to = new Date(2026, 8, 30);
      facade.filterForm.patchValue({ dateFrom: from, dateTo: to });
      jest.advanceTimersByTime(500);

      const args = referralClient.getPaged.mock.calls.at(-1);
      expect(args?.[1]).toBe(from);
      expect(args?.[2]).toBe(to);
      expect(facade.filters.chips()).toEqual([
        {
          key: 'dateRange',
          label: 'pages.loyalty_referrals.filter.date_range',
          value: '1. 9. 2026 – 30. 9. 2026',
          controls: ['dateFrom', 'dateTo'],
        },
      ]);

      facade.filters.removeChip('dateRange');
      const cleared = referralClient.getPaged.mock.calls.at(-1);
      expect(cleared?.[1]).toBeUndefined();
      expect(cleared?.[2]).toBeUndefined();
      expect(facade.filters.chips()).toEqual([]);
    });
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
    // Every member of a generated command is optional, so a dropped assignment
    // type-checks — pin the serialized body instead (ADR-0031).
    const [id, command] = referralClient.reverse.mock.calls[0];
    expect(id).toBe('ref-1');
    expect(command).toBeInstanceOf(ReverseReferralCommand);
    expect(command.toJSON()).toEqual({
      referralId: 'ref-1',
      reason: 'fraud ring',
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
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

  it('leaves the referral.not_qualified refusal to the interceptor toast on reverse failure', () => {
    referralClient.reverse.mockReturnValue(
      throwError(() => ({ result: { detail: 'referral.not_qualified' } }))
    );

    facade.reverseReferral('ref-1', 'reason', jest.fn());

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
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
    expect(command).toBeInstanceOf(ForceQualifyReferralCommand);
    expect(command.toJSON()).toEqual({
      referralId: 'ref-2',
      reason: 'legit order confirmed',
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_force_qualify'
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });

  it('leaves the referral.not_accepted refusal to the interceptor toast on force-qualify failure', () => {
    referralClient.forceQualify.mockReturnValue(
      throwError(() => ({ result: { detail: 'referral.not_accepted' } }))
    );

    facade.forceQualifyReferral('ref-2', 'reason', jest.fn());

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves the referral.reason_required refusal to the interceptor toast on intervention failure', () => {
    referralClient.reverse.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'referral.reason_required' }),
      }))
    );

    facade.reverseReferral('ref-1', 'reason', jest.fn());

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves an unknown refusal to the interceptor toast', () => {
    referralClient.reverse.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.reverseReferral('ref-1', 'reason', jest.fn());

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('ignores a second intervention while one is in flight', () => {
    facade.intervening.set(true);

    facade.reverseReferral('ref-1', 'reason', jest.fn());
    facade.forceQualifyReferral('ref-2', 'reason', jest.fn());

    expect(referralClient.reverse).not.toHaveBeenCalled();
    expect(referralClient.forceQualify).not.toHaveBeenCalled();
  });
});
