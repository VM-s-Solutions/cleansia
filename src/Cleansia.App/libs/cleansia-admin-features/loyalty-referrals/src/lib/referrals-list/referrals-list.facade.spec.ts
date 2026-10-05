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
import { formatMoney } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { ReferralsListFacade } from './referrals-list.facade';

const czk = (value: number) => formatMoney(value, 'CZK', 'cs-CZ', { fractionDigits: 2 });

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
      getPaged: jest.fn().mockReturnValue(of(page)),
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

    it('asks the server for held referrals alone under the held filter, with no status of its own', () => {
      facade.filterForm.patchValue({ status: 'held' });
      jest.advanceTimersByTime(500);

      const args = referralClient.getPaged.mock.calls.at(-1);
      expect(args?.[0]).toBeUndefined();
      expect(args?.[5]).toBe(true);
      expect(facade.filters.chips()).toEqual([
        {
          key: 'status',
          label: 'pages.loyalty_referrals.filter.status',
          value: 'pages.loyalty_referrals.filter.status_held',
        },
      ]);
    });

    it('leaves the hold out of every other filter, so accepted still lists the held rows', () => {
      facade.filterForm.patchValue({ status: 'accepted' });
      jest.advanceTimersByTime(500);

      const args = referralClient.getPaged.mock.calls.at(-1);
      expect(args?.[0]).toBe(ReferralStatus.Accepted);
      expect(args?.[5]).toBeUndefined();
    });

    it('offers the held filter among the statuses', () => {
      expect(facade.statusFilterOptions().map((option) => option.value)).toContain('held');
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
          creditTakenFromReferrer: 150,
          referrerCurrencyCode: 'CZK',
          creditTakenFromReferred: 40.5,
          referredCurrencyCode: 'CZK',
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
      expectHeld: false,
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_reverse',
      { referrer: czk(150), referred: czk(40.5) }
    );
    expect(referralClient.getPaged).toHaveBeenCalledTimes(1);
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.intervening()).toBe(false);
  });

  it('says no credit was taken back when the reversed referral had paid none', () => {
    referralClient.reverse.mockReturnValue(
      of(
        ReverseReferralResponse.fromJS({
          referralId: 'ref-1',
          creditTakenFromReferrer: 0,
          referrerCurrencyCode: null,
          creditTakenFromReferred: 0,
          referredCurrencyCode: null,
        })
      )
    );
    referralClient.getPaged.mockReturnValue(of(page));

    facade.reverseReferral('ref-1', 'fraud ring', jest.fn());

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_reverse_no_credit'
    );
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
          creditGrantedToReferrer: 150,
          referrerCurrencyCode: 'CZK',
          creditGrantedToReferred: 150,
          referredCurrencyCode: 'CZK',
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
      expectHeld: false,
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_force_qualify',
      { referrer: czk(150), referred: czk(150) }
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });

  it('releases a held referral through force-qualify, saying it saw the hold, and reloads on success', () => {
    referralClient.forceQualify.mockReturnValue(
      of(
        ForceQualifyReferralResponse.fromJS({
          referralId: 'ref-4',
          creditGrantedToReferrer: 150,
          referrerCurrencyCode: 'CZK',
          creditGrantedToReferred: 150,
          referredCurrencyCode: 'CZK',
        })
      )
    );
    referralClient.getPaged.mockReturnValue(of(page));
    const onSuccess = jest.fn();

    facade.releaseReferral('ref-4', '  separate flats, checked  ', onSuccess);

    const [id, command] = referralClient.forceQualify.mock.calls[0];
    expect(id).toBe('ref-4');
    expect(command).toBeInstanceOf(ForceQualifyReferralCommand);
    expect(command.toJSON()).toEqual({
      referralId: 'ref-4',
      reason: 'separate flats, checked',
      expectHeld: true,
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_force_qualify',
      { referrer: czk(150), referred: czk(150) }
    );
    expect(referralClient.getPaged).toHaveBeenCalledTimes(1);
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.intervening()).toBe(false);
  });

  it('does not call force-qualify to release with a blank reason', () => {
    facade.releaseReferral('ref-4', '   ', jest.fn());
    expect(referralClient.forceQualify).not.toHaveBeenCalled();
  });

  it('re-reads the list when the hold changed since it was loaded, so the row shows where it stands', () => {
    const holdChanged = () => throwError(() => ({ result: { detail: 'referral.hold_changed' } }));
    referralClient.getPaged.mockReturnValue(of(page));
    referralClient.forceQualify.mockReturnValue(holdChanged());
    referralClient.reverse.mockReturnValue(holdChanged());
    const onSuccess = jest.fn();

    facade.forceQualifyReferral('ref-2', 'reason', onSuccess);
    expect(referralClient.getPaged).toHaveBeenCalledTimes(1);

    facade.rejectReferral('ref-3', 'reason', onSuccess);
    expect(referralClient.getPaged).toHaveBeenCalledTimes(2);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.intervening()).toBe(false);
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

  it('rejects a held referral through reverse, says nothing was paid, and reloads', () => {
    referralClient.reverse.mockReturnValue(
      of(
        ReverseReferralResponse.fromJS({
          referralId: 'ref-3',
          creditTakenFromReferrer: 0,
          referrerCurrencyCode: null,
          creditTakenFromReferred: 0,
          referredCurrencyCode: null,
        })
      )
    );
    referralClient.getPaged.mockReturnValue(of(page));
    const onSuccess = jest.fn();

    facade.rejectReferral('ref-3', '  same flat, one household  ', onSuccess);

    const [id, command] = referralClient.reverse.mock.calls[0];
    expect(id).toBe('ref-3');
    expect(command).toBeInstanceOf(ReverseReferralCommand);
    expect(command.toJSON()).toEqual({
      referralId: 'ref-3',
      reason: 'same flat, one household',
      expectHeld: true,
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_reject'
    );
    expect(referralClient.getPaged).toHaveBeenCalledTimes(1);
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.intervening()).toBe(false);
  });

  it('says what was taken back when a rejection lands on a referral another administrator had already released', () => {
    referralClient.reverse.mockReturnValue(
      of(
        ReverseReferralResponse.fromJS({
          referralId: 'ref-3',
          creditTakenFromReferrer: 150,
          referrerCurrencyCode: 'CZK',
          creditTakenFromReferred: 150,
          referredCurrencyCode: 'CZK',
        })
      )
    );
    referralClient.getPaged.mockReturnValue(of(page));

    facade.rejectReferral('ref-3', 'reason', jest.fn());

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.loyalty_referrals.intervention.success_reverse',
      { referrer: czk(150), referred: czk(150) }
    );
  });

  it('does not call reverse to reject with a blank reason', () => {
    facade.rejectReferral('ref-3', '   ', jest.fn());
    expect(referralClient.reverse).not.toHaveBeenCalled();
  });

  it('re-reads the list when an intervention is refused, and keeps the dialog open', () => {
    referralClient.getPaged.mockReturnValue(of(page));
    referralClient.forceQualify.mockReturnValue(
      throwError(() => ({ result: { detail: 'referral.not_accepted' } }))
    );
    referralClient.reverse.mockReturnValue(
      throwError(() => ({ result: { detail: 'referral.not_qualified' } }))
    );
    const onSuccess = jest.fn();

    facade.forceQualifyReferral('ref-2', 'reason', onSuccess);
    expect(referralClient.getPaged).toHaveBeenCalledTimes(1);

    facade.rejectReferral('ref-3', 'reason', onSuccess);
    expect(referralClient.getPaged).toHaveBeenCalledTimes(2);

    facade.reverseReferral('ref-1', 'reason', onSuccess);
    expect(referralClient.getPaged).toHaveBeenCalledTimes(3);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(facade.intervening()).toBe(false);
  });

  it('ignores a second intervention while one is in flight', () => {
    facade.intervening.set(true);

    facade.reverseReferral('ref-1', 'reason', jest.fn());
    facade.rejectReferral('ref-3', 'reason', jest.fn());
    facade.forceQualifyReferral('ref-2', 'reason', jest.fn());
    facade.releaseReferral('ref-4', 'reason', jest.fn());

    expect(referralClient.reverse).not.toHaveBeenCalled();
    expect(referralClient.forceQualify).not.toHaveBeenCalled();
  });
});
