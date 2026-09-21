import { getFiscalErrorKindBadge, getFiscalErrorKindClass } from './fiscal-failures-list.models';

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

describe('getFiscalErrorKindClass', () => {
  it.each([
    [1, 'status-badge status-badge--warning'],
    [2, 'status-badge status-badge--danger'],
    [3, 'status-badge status-badge--danger'],
    [4, 'status-badge status-badge--neutral'],
  ])('draws wire value %i as the shared pill %s', (wireValue, className) => {
    expect(getFiscalErrorKindClass(wireValue)).toBe(className);
  });

  it('draws no pill for None or a missing kind', () => {
    expect(getFiscalErrorKindClass(0)).toBe('');
    expect(getFiscalErrorKindClass(undefined)).toBe('');
  });
});
