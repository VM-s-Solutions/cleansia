import { GetMembershipPlansResponse, GetMyMembershipResponse } from '../client/customer-client';
import { offeredTrialDays } from './membership-trial-offer';

function plan(trialPeriodDays: number): GetMembershipPlansResponse {
  return GetMembershipPlansResponse.fromJS({ code: 'PLUS_MONTHLY', trialPeriodDays });
}

function membership(hasMembership: boolean, trialEligible: boolean): GetMyMembershipResponse {
  return GetMyMembershipResponse.fromJS({ hasMembership, trialEligible });
}

describe('offeredTrialDays', () => {
  it("offers the plan's own trial to a visitor the server has not answered for", () => {
    expect(offeredTrialDays(plan(14), null)).toBe(14);
  });

  it('offers it to a signed-in customer who has never had a trial', () => {
    expect(offeredTrialDays(plan(7), membership(false, true))).toBe(7);
  });

  it('offers none to a customer who has had their one trial', () => {
    expect(offeredTrialDays(plan(14), membership(false, false))).toBe(0);
  });

  it('offers none to a customer who is already a member', () => {
    expect(offeredTrialDays(plan(14), membership(true, true))).toBe(0);
  });

  it('offers none on a plan without a trial, or when there is no plan', () => {
    expect(offeredTrialDays(plan(0), null)).toBe(0);
    expect(offeredTrialDays(null, null)).toBe(0);
  });
});
