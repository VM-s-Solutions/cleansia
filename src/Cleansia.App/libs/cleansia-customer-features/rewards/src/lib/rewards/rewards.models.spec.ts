import { LoyaltyEarnSource } from '@cleansia/customer-services';
import { movementLabelKey } from './rewards.models';

// Every refund of a completed order, partial, full or a dispute's, claws its points back as a Revoke
// written with OrderPartiallyRefunded; only a cancellation writes OrderCancelled.
describe('movementLabelKey', () => {
  it.each([
    [LoyaltyEarnSource.OrderCompleted, 'pages.rewards.tx.completed'],
    [LoyaltyEarnSource.OrderCancelled, 'pages.rewards.tx.cancelled'],
    [LoyaltyEarnSource.OrderPartiallyRefunded, 'pages.rewards.tx.refunded'],
    [LoyaltyEarnSource.Referral, 'pages.rewards.tx.referral'],
    [LoyaltyEarnSource.ManualGrant, 'pages.rewards.tx.manual'],
    [LoyaltyEarnSource.ManualRevoke, 'pages.rewards.tx.manual'],
  ])('labels source %s as %s', (source, key) => {
    expect(movementLabelKey(source)).toBe(key);
  });

  it('labels a source this build does not know as a manual adjustment', () => {
    expect(movementLabelKey(99 as LoyaltyEarnSource)).toBe('pages.rewards.tx.manual');
  });
});
