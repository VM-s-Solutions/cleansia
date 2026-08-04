import { GetMyMembershipResponse } from '../client/customer-client';

/**
 * What a customer surface may say about the express surcharge, derived from
 * `GetMyMembership`. Lives here rather than in a feature lib because the booking wizard and the
 * membership screens must not answer it two different ways.
 *
 *  - 'none'      → no active membership, or a plan that carries no express quota. Say nothing.
 *  - 'trial'     → the plan carries a quota but the trial has not converted to a paid month yet,
 *                  so nothing is waived. `expressUpgradesRemaining` is 0 for exactly the same
 *                  reason an exhausted member's is, and only `trialEndsAtUtc` tells them apart.
 *  - 'available' → at least one waiver left in the current calendar month.
 *  - 'exhausted' → the quota is used up until the calendar month rolls over.
 */
export type ExpressWaiverStatus = 'none' | 'trial' | 'available' | 'exhausted';

export function resolveExpressWaiverStatus(
  membership: GetMyMembershipResponse | null,
  nowUtc: Date,
): ExpressWaiverStatus {
  if (!membership?.hasMembership) return 'none';
  if ((membership.expressUpgradesPerMonth ?? 0) <= 0) return 'none';

  const trialEndsAtUtc = membership.trialEndsAtUtc;
  if (trialEndsAtUtc && trialEndsAtUtc.getTime() > nowUtc.getTime()) return 'trial';

  return (membership.expressUpgradesRemaining ?? 0) > 0 ? 'available' : 'exhausted';
}
