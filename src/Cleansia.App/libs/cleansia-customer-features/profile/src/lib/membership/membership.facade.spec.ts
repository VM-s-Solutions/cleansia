import { TestBed } from '@angular/core/testing';
import {
  CreateMembershipCheckoutSessionCommand,
  CustomerClient,
  GetMyMembershipResponse,
  SwapMembershipPlanCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { of, throwError } from 'rxjs';
import { MembershipFacade } from './membership.facade';

function buildMembership(fields: {
  hasMembership?: boolean;
  expressUpgradesPerMonth?: number;
  expressUpgradesRemaining?: number;
  trialEndsAtUtc?: Date;
}): GetMyMembershipResponse {
  const response = new GetMyMembershipResponse();
  response.hasMembership = fields.hasMembership ?? true;
  response.expressUpgradesPerMonth = fields.expressUpgradesPerMonth;
  response.expressUpgradesRemaining = fields.expressUpgradesRemaining;
  response.trialEndsAtUtc = fields.trialEndsAtUtc;
  return response;
}

describe('MembershipFacade — express waiver state', () => {
  let facade: MembershipFacade;
  let membershipClient: {
    getMine: jest.Mock;
    swapPlan: jest.Mock;
    createCheckoutSession: jest.Mock;
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
      createCheckoutSession: jest.fn(),
    };
    snackbar = {
      showApiError: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        MembershipFacade,
        { provide: CustomerClient, useValue: { membershipClient } },
        { provide: SnackbarService, useValue: snackbar },
      ],
    });

    facade = TestBed.inject(MembershipFacade);
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

  it('advertises the perk as pending — not exhausted — during the trial', () => {
    membershipClient.getMine.mockReturnValue(
      of(
        buildMembership({
          expressUpgradesPerMonth: 2,
          expressUpgradesRemaining: 0,
          trialEndsAtUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
        }),
      ),
    );

    facade.refresh();

    expect(facade.expressWaiverPendingTrial()).toBe(true);
    expect(facade.expressWaiverExhausted()).toBe(false);
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

    it('serializes a checkout session with the plan code and both return urls', () => {
      membershipClient.createCheckoutSession.mockReturnValue(of(null));

      facade.createCheckoutSession(
        'plus-monthly',
        'https://app.test/success',
        'https://app.test/cancel',
      );

      const command: CreateMembershipCheckoutSessionCommand =
        membershipClient.createCheckoutSession.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateMembershipCheckoutSessionCommand);
      expect(command.toJSON()).toEqual({
        planCode: 'plus-monthly',
        successUrl: 'https://app.test/success',
        cancelUrl: 'https://app.test/cancel',
      });
    });
  });
});
