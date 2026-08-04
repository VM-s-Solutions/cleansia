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

const NOW = new Date('2026-08-04T10:00:00Z');

describe('resolveExpressWaiverStatus', () => {
  it('is none without a membership', () => {
    expect(resolveExpressWaiverStatus(null, NOW)).toBe('none');
    expect(resolveExpressWaiverStatus(buildMembership({ hasMembership: false }), NOW)).toBe(
      'none',
    );
  });

  it('is none when the plan carries no express quota', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({ expressUpgradesPerMonth: 0, expressUpgradesRemaining: 0 }),
      NOW,
    );

    expect(status).toBe('none');
  });

  it('is available while waivers remain', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 1 }),
      NOW,
    );

    expect(status).toBe('available');
  });

  it('is exhausted once the remaining count hits zero', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({ expressUpgradesPerMonth: 2, expressUpgradesRemaining: 0 }),
      NOW,
    );

    expect(status).toBe('exhausted');
  });

  it('is trial — not exhausted — for a zero remaining count inside the trial', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({
        expressUpgradesPerMonth: 2,
        expressUpgradesRemaining: 0,
        trialEndsAtUtc: new Date('2026-08-18T10:00:00Z'),
      }),
      NOW,
    );

    expect(status).toBe('trial');
  });

  it('leaves the trial state the instant the trial ends', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({
        expressUpgradesPerMonth: 2,
        expressUpgradesRemaining: 2,
        trialEndsAtUtc: NOW,
      }),
      NOW,
    );

    expect(status).toBe('available');
  });

  it('is exhausted, not trial, once a past trial has converted', () => {
    const status = resolveExpressWaiverStatus(
      buildMembership({
        expressUpgradesPerMonth: 2,
        expressUpgradesRemaining: 0,
        trialEndsAtUtc: new Date('2026-07-21T10:00:00Z'),
      }),
      NOW,
    );

    expect(status).toBe('exhausted');
  });
});
