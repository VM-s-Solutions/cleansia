import { getFiscalErrorKindBadge } from './fiscal-failures-list.models';

describe('getFiscalErrorKindBadge', () => {
  it.each([
    [1, 'transient'],
    [2, 'permanent'],
    [3, 'configuration'],
    [4, 'unknown'],
  ])('maps wire value %i to the %s badge', (wireValue, badge) => {
    expect(getFiscalErrorKindBadge(wireValue)).toBe(badge);
  });

  it('returns no badge for None (0)', () => {
    expect(getFiscalErrorKindBadge(0)).toBeUndefined();
  });

  it('returns no badge for a missing kind', () => {
    expect(getFiscalErrorKindBadge(undefined)).toBeUndefined();
  });

  it('returns no badge for an unmapped wire value', () => {
    expect(getFiscalErrorKindBadge(99)).toBeUndefined();
  });
});
