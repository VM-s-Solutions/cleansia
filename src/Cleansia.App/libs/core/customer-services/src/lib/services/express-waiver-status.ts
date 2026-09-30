import { GetMyMembershipResponse } from '../client/customer-client';

/**
 * What a customer surface may say about the express surcharge, derived from
 * `GetMyMembership`. Lives here rather than in a feature lib because the booking wizard and the
 * membership screens must not answer it two different ways.
 *
 *  - 'none'      → no active membership, or a plan that carries no express quota. Say nothing.
 *  - 'available' → at least one waiver left in the current calendar month.
 *  - 'exhausted' → the quota is used up until the calendar month rolls over.
 */
export type ExpressWaiverStatus = 'none' | 'available' | 'exhausted';

export function resolveExpressWaiverStatus(
  membership: GetMyMembershipResponse | null,
): ExpressWaiverStatus {
  if (!membership?.hasMembership) return 'none';
  if ((membership.expressUpgradesPerMonth ?? 0) <= 0) return 'none';

  return (membership.expressUpgradesRemaining ?? 0) > 0 ? 'available' : 'exhausted';
}
