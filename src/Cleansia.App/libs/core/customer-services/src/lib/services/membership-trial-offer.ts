import { GetMembershipPlansResponse, GetMyMembershipResponse } from '../client/customer-client';

/**
 * The free days a subscribe surface may promise on a plan. One trial per account: `trialEligible`
 * is the server's own check, and a null membership is a visitor it has not answered for.
 */
export function offeredTrialDays(
  plan: GetMembershipPlansResponse | null | undefined,
  membership: GetMyMembershipResponse | null,
): number {
  if (membership?.hasMembership || membership?.trialEligible === false) return 0;
  return plan?.trialPeriodDays ?? 0;
}
