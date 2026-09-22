import { legendBadgeClass } from './status-legend';

describe('legendBadgeClass', () => {
  it('draws the legend pill in the tone the table badge uses for that status', () => {
    expect(legendBadgeClass('order', 'Completed')).toBe('status-badge status-badge--success');
    expect(legendBadgeClass('payment', 'Failed')).toBe('status-badge status-badge--danger');
    expect(legendBadgeClass('invoice', 'Paid')).toBe('status-badge status-badge--success');
  });

  it('falls back to the neutral tone for a member the badge does not know', () => {
    expect(legendBadgeClass('order', 'NotAStatus')).toBe('status-badge status-badge--neutral');
  });
});
