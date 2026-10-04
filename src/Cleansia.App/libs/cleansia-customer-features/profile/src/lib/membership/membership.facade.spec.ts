import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  MembershipStatus,
  SwapMembershipPlanCommand,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { DialogService, SnackbarService } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { of, throwError } from 'rxjs';
import { MembershipFacade } from './membership.facade';

function buildMembership(fields: {
  hasMembership?: boolean;
  expressUpgradesPerMonth?: number;
  expressUpgradesRemaining?: number;
  trialEndsAtUtc?: Date;
  currencyCode?: string;
  status?: MembershipStatus;
}): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = fields.hasMembership ?? true;
  response.status = fields.status ?? MembershipStatus.Active;
  response.expressUpgradesPerMonth = fields.expressUpgradesPerMonth;
  response.expressUpgradesRemaining = fields.expressUpgradesRemaining;
  response.trialEndsAtUtc = fields.trialEndsAtUtc;
  response.currencyCode = fields.currencyCode;
  return response;
}

function buildPlan(code: string, currencyCode: string): GetMembershipPlansResponse {
  const plan = new GetMembershipPlansResponse();
  plan.code = code;
  plan.currencyCode = currencyCode;
  return plan;
}

describe('MembershipFacade — express waiver state', () => {
  let facade: MembershipFacade;
  let membershipClient: {
    getMine: jest.Mock;
    swapPlan: jest.Mock;
    getPlans: jest.Mock;
    cancel: jest.Mock;
  };
  let snackbar: {
    showApiError: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  beforeEach(() => {
    membershipClient = {
      getMine: jest.fn(),
      swapPlan: jest.fn(),
      getPlans: jest.fn().mockReturnValue(of([])),
      cancel: jest.fn().mockReturnValue(of(undefined)),
    };
    snackbar = {
      showApiError: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        MembershipFacade,
        provideMockStore({
          selectors: [{ selector: selectMarketCountryId, value: 'svk-id' }],
        }),
        { provide: CustomerClient, useValue: { membershipClient } },
        { provide: SnackbarService, useValue: snackbar },
        // The confirms are pinned by the screen's spec, through the real DialogService.
        { provide: DialogService, useValue: {} },
      ],
    });

    facade = TestBed.inject(MembershipFacade);
  });

  // A subscription keeps its currency for life (ADR-0059 D2): every figure on this screen is
  // labelled with `GetMine.currencyCode`, whatever market the customer is browsing in now.
  describe('the currency the membership is billed in', () => {
    it("is the membership response's own code, not the market's", () => {
      membershipClient.getMine.mockReturnValue(of(buildMembership({ currencyCode: 'CZK' })));

      facade.refresh();

      expect(facade.currencyCode()).toBe('CZK');
    });

    it('is unknown until the membership is loaded', () => {
      expect(facade.currencyCode()).toBeNull();
    });

    it('reads the plans for the chosen market', () => {
      facade.loadPlans();

      expect(membershipClient.getPlans).toHaveBeenCalledWith('svk-id');
    });

    // A swap is settled in the membership's currency (the server picks that row), so a plan the
    // market prices in another currency cannot be offered at the price the market shows.
    it("offers only the plans priced in the membership's currency", () => {
      membershipClient.getMine.mockReturnValue(of(buildMembership({ currencyCode: 'CZK' })));
      membershipClient.getPlans.mockReturnValue(
        of([buildPlan('PLUS_MONTHLY', 'EUR'), buildPlan('PLUS_YEARLY', 'EUR')]),
      );

      facade.refresh();
      facade.loadPlans();

      expect(facade.switchablePlans()).toEqual([]);

      membershipClient.getPlans.mockReturnValue(
        of([buildPlan('PLUS_MONTHLY', 'CZK'), buildPlan('PLUS_YEARLY', 'CZK')]),
      );
      facade.loadPlans();

      expect(facade.switchablePlans().map((p) => p.code)).toEqual(['PLUS_MONTHLY', 'PLUS_YEARLY']);
    });
  });

  // A running trial carries every Plus benefit. The screens branch on it only to say when the first
  // payment falls and that nothing is charged before then.
  describe('a running trial', () => {
    const DAY_MS = 24 * 60 * 60 * 1000;

    it('is the date the trial ends while it is still running', () => {
      const trialEndsAtUtc = new Date(Date.now() + 7 * DAY_MS);
      membershipClient.getMine.mockReturnValue(of(buildMembership({ trialEndsAtUtc })));

      facade.refresh();

      expect(facade.trialEndsOn()).toEqual(trialEndsAtUtc);
    });

    it('is nothing once the trial has ended', () => {
      membershipClient.getMine.mockReturnValue(
        of(buildMembership({ trialEndsAtUtc: new Date(Date.now() - DAY_MS) })),
      );

      facade.refresh();

      expect(facade.trialEndsOn()).toBeNull();
    });

    it('is nothing for a membership that never had a trial', () => {
      membershipClient.getMine.mockReturnValue(of(buildMembership({})));

      facade.refresh();

      expect(facade.trialEndsOn()).toBeNull();
    });

    it('is nothing for a customer with no membership', () => {
      membershipClient.getMine.mockReturnValue(
        of(
          buildMembership({
            hasMembership: false,
            trialEndsAtUtc: new Date(Date.now() + 7 * DAY_MS),
          }),
        ),
      );

      facade.refresh();

      expect(facade.trialEndsOn()).toBeNull();
    });

    it('is nothing before the membership is loaded', () => {
      expect(facade.trialEndsOn()).toBeNull();
    });

    // The cancel dialog, its success toast and the switch dialog are built in code, not in the
    // templates the claim spec reads, so they are pinned here.
    describe('what cancelling or switching tells the member', () => {
      const trialing = () =>
        buildMembership({ trialEndsAtUtc: new Date(Date.now() + 7 * DAY_MS) });

      it('tells a trialing member that nothing is charged before the trial ends', () => {
        membershipClient.getMine.mockReturnValue(of(trialing()));
        facade.refresh();

        facade.cancel();

        expect(facade.cancelDialogMessageKey()).toBe('pages.membership.cancel_dialog_message_trial');
        expect(facade.switchDialogMessageKey()).toBe('pages.membership.switch_dialog_message_trial');
        expect(facade.switchLeadKey()).toBe('pages.membership.switch_lead_trial');
        expect(facade.switchConfirmKey()).toBe('pages.membership.switch_dialog_confirm_trial');
        expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
          'pages.membership.cancel_success_trial',
        );
      });

      it('tells a paid member that the paid period runs to its end', () => {
        membershipClient.getMine.mockReturnValue(of(buildMembership({})));
        facade.refresh();

        facade.cancel();

        expect(facade.cancelDialogMessageKey()).toBe('pages.membership.cancel_dialog_message');
        expect(facade.switchDialogMessageKey()).toBe('pages.membership.switch_dialog_message');
        expect(facade.switchLeadKey()).toBe('pages.membership.switch_lead');
        expect(facade.switchConfirmKey()).toBe('pages.membership.switch_dialog_confirm');
        expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
          'pages.membership.cancel_success',
        );
      });

      it('tells a member whose trial has ended what a paid member is told', () => {
        membershipClient.getMine.mockReturnValue(
          of(buildMembership({ trialEndsAtUtc: new Date(Date.now() - DAY_MS) })),
        );
        facade.refresh();

        facade.cancel();

        expect(facade.cancelDialogMessageKey()).toBe('pages.membership.cancel_dialog_message');
        expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
          'pages.membership.cancel_success',
        );
      });
    });
  });

  // A failed renewal keeps the enrolment, so no second subscription starts, but every benefit is
  // off and the server cancels it now rather than at the period end.
  describe('a renewal payment that failed', () => {
    const pastDue = () => buildMembership({ status: MembershipStatus.PastDue });

    it('is what the page shows for a past-due membership', () => {
      membershipClient.getMine.mockReturnValue(of(pastDue()));

      facade.refresh();

      expect(facade.paymentFailed()).toBe(true);
    });

    it('is not shown for an active membership, nor before one is loaded', () => {
      expect(facade.paymentFailed()).toBe(false);

      membershipClient.getMine.mockReturnValue(of(buildMembership({})));
      facade.refresh();

      expect(facade.paymentFailed()).toBe(false);
    });

    it('tells the member the cancel ends it now with nothing more charged, then toasts the same', () => {
      membershipClient.getMine.mockReturnValue(of(pastDue()));
      facade.refresh();

      facade.cancel();

      expect(membershipClient.cancel).toHaveBeenCalledTimes(1);
      expect(facade.cancelDialogMessageKey()).toBe(
        'pages.membership.cancel_dialog_message_past_due',
      );
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.membership.cancel_success_past_due',
      );
    });

    it('voices a refused cancel and stops the spinner', () => {
      membershipClient.getMine.mockReturnValue(of(pastDue()));
      membershipClient.cancel.mockReturnValue(throwError(() => new Error('boom')));
      facade.refresh();

      facade.cancel();

      expect(facade.cancelling()).toBe(false);
      expect(snackbar.showApiError).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    });
  });

  it('advertises nothing before the membership is loaded', () => {
    expect(facade.expressWaiverAdvertised()).toBe(false);
    expect(facade.expressUpgradesRemaining()).toBe(0);
  });

  it('advertises the remaining count for a paid member with waivers left', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 2 })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(true);
    expect(facade.expressWaiverAvailable()).toBe(true);
    expect(facade.expressUpgradesRemaining()).toBe(2);
    expect(facade.loading()).toBe(false);
  });

  it('advertises the perk as exhausted rather than absent when the quota is used up', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 0 })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(true);
    expect(facade.expressWaiverExhausted()).toBe(true);
    expect(facade.expressWaiverAvailable()).toBe(false);
  });

  it('advertises the remaining count for a member inside the free trial', () => {
    membershipClient.getMine.mockReturnValue(
      of(
        buildMembership({
          expressUpgradesPerMonth: 2,
          expressUpgradesRemaining: 2,
          trialEndsAtUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
        }),
      ),
    );

    facade.refresh();

    expect(facade.expressWaiverAvailable()).toBe(true);
    expect(facade.expressUpgradesRemaining()).toBe(2);
  });

  it('advertises nothing on a plan that carries no express quota', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ expressUpgradesPerMonth: 0 })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(false);
  });

  it('advertises nothing for a customer with no active membership', () => {
    membershipClient.getMine.mockReturnValue(
      of(buildMembership({ hasMembership: false })),
    );

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(false);
  });

  it('advertises nothing and surfaces the error when the read fails', () => {
    membershipClient.getMine.mockReturnValue(throwError(() => new Error('boom')));

    facade.refresh();

    expect(facade.expressWaiverAdvertised()).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(snackbar.showApiError).toHaveBeenCalledTimes(1);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a plan swap with the target plan code', () => {
      membershipClient.swapPlan.mockReturnValue(of(undefined));
      membershipClient.getMine.mockReturnValue(of(buildMembership({})));

      facade.swapPlan('plus-yearly');

      const command: SwapMembershipPlanCommand =
        membershipClient.swapPlan.mock.calls[0][0];
      expect(command).toBeInstanceOf(SwapMembershipPlanCommand);
      expect(command.toJSON()).toEqual({ newPlanCode: 'plus-yearly' });
    });
  });

  /**
   * THE GENERATED CLIENT CAN ANSWER WITH NULL. Its declared type is
   * `GetMembershipPlansResponse[]`, but `processGetPlans` falls to `result200 = null as any` for any
   * 200 whose body is not a JSON array — an empty body, a `{}`, a 204. Null is not an error, so
   * `catchError` never fires, and the declared type means TypeScript never complains either. Five
   * readers then index or measure this signal.
   */
  it('holds an empty list, and hands the callback one, when the client answers null', () => {
    membershipClient.getPlans.mockReturnValue(of(null));
    const onLoaded = jest.fn();

    facade.loadPlans(onLoaded);

    expect(facade.plans()).toEqual([]);
    // The callback's argument is indexed by its callers just as the signal is, so both halves are
    // pinned — which is why the facade coalesces once and passes the same list to both.
    expect(onLoaded).toHaveBeenCalledWith([]);
  });

});
