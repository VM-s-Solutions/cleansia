import { LoyaltyEarnSource } from '@cleansia/customer-services';

/**
 * Read off the source alone: a Revoke is a cancellation, a refund or an admin's adjustment depending
 * on what wrote it, and every refund of a completed order (partial, full or a dispute's) writes
 * OrderPartiallyRefunded.
 */
export function movementLabelKey(source: LoyaltyEarnSource): string {
  switch (source) {
    case LoyaltyEarnSource.OrderCompleted:
      return 'pages.rewards.tx.completed';
    case LoyaltyEarnSource.OrderCancelled:
      return 'pages.rewards.tx.cancelled';
    case LoyaltyEarnSource.OrderPartiallyRefunded:
      return 'pages.rewards.tx.refunded';
    case LoyaltyEarnSource.Referral:
      return 'pages.rewards.tx.referral';
    default:
      return 'pages.rewards.tx.manual';
  }
}
