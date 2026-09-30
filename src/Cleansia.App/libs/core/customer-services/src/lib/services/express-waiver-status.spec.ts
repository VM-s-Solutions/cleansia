import { GetMyMembershipResponse } from '../client/customer-client';
import { resolveExpressWaiverStatus } from './express-waiver-status';

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

describe('resolveExpressWaiverStatus', () => {
  it('is none without a membership', () => {
    expect(resolveExpressWaiverStatus(null)).toBe('none');
    expect(resolveExpressWaiverStatus(buildMembership({ hasMembership: false }))).toBe(
      'none',
    );
  });

  it('is none when the plan carries no express quota', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({ expressUpgradesPerMonth: 0, expressUpgradesRemaining: 0 }),
    );

    expect(status).toBe('none');
  });

  it('is available while waivers remain', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 1 }),
    );

    expect(status).toBe('available');
  });

  it('is exhausted once the remaining count hits zero', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 0 }),
    );

    expect(status).toBe('exhausted');
  });

  it("counts the waivers of a member inside the free trial like a paying member's", () => {
    const trialing = (expressUpgradesRemaining: number) =>
      buildMembership({
        expressUpgradesPerMonth: 2,
        expressUpgradesRemaining,
        trialEndsAtUtc: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
      });

    expect(resolveExpressWaiverStatus(trialing(2))).toBe('available');
    expect(resolveExpressWaiverStatus(trialing(0))).toBe('exhausted');
  });
});
