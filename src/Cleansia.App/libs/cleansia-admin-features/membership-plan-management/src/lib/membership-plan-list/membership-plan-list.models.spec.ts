import { MembershipPlanListItem } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import {
  BILLING_INTERVAL_WIRE,
  UNPRICED_PLAN_CELL,
  formatPlanPrice,
  getMembershipPlanTableDefinition,
  resolveMembershipPlanErrorKey,
  toBillingIntervalWireValue,
} from './membership-plan-list.models';

describe('formatPlanPrice', () => {
  it('prints the amount with the code the row carries', () => {
    expect(formatPlanPrice(199, 'CZK')).toBe('199.00 CZK');
    expect(formatPlanPrice(2030.5, 'EUR')).toBe('2030.50 EUR');
  });

  it('prints a dash, never zero, for a plan with no row in the default currency', () => {
    expect(formatPlanPrice(null, 'CZK')).toBe(UNPRICED_PLAN_CELL);
    expect(formatPlanPrice(undefined, 'CZK')).toBe(UNPRICED_PLAN_CELL);
  });

  it('prints a bare number rather than a currency it cannot name', () => {
    expect(formatPlanPrice(199, undefined)).toBe('199.00');
  });
});

describe('getMembershipPlanTableDefinition', () => {
  const translate = { instant: (k: string) => k } as TranslateService;
  const { columns } = getMembershipPlanTableDefinition(
    { onEdit: jest.fn(), onDeactivate: jest.fn() },
    translate
  );
  const column = (id: string) => {
    const found = columns.find((c) => c.id === id);
    if (!found?.getValue) throw new Error(`column ${id} missing or static`);
    return found;
  };

  it('renders a priced row with its own currency code in both money columns and the currency column', () => {
    const row = MembershipPlanListItem.fromJS({
      price: 2030,
      monthlyEquivalentPrice: 169.17,
      currencyCode: 'CZK',
    });

    expect(column('price').getValue?.(row)).toBe('2030.00 CZK');
    expect(column('monthlyEquivalentPrice').getValue?.(row)).toBe('169.17 CZK');
    expect(column('currencyCode').getValue?.(row)).toBe('CZK');
  });

  it('renders an unpriced row as dashes while the currency column still names the default code', () => {
    const row = MembershipPlanListItem.fromJS({ currencyCode: 'CZK' });

    expect(column('price').getValue?.(row)).toBe(UNPRICED_PLAN_CELL);
    expect(column('monthlyEquivalentPrice').getValue?.(row)).toBe(UNPRICED_PLAN_CELL);
    expect(column('currencyCode').getValue?.(row)).toBe('CZK');
  });

  it('offers no price sort — the sortable field list is not part of the admin contract', () => {
    expect(columns.every((c) => !('sortable' in c && c.sortable))).toBe(true);
  });
});

describe('toBillingIntervalWireValue', () => {
  it('maps the numeric wire value and defaults to monthly', () => {
    expect(toBillingIntervalWireValue(2)).toBe(BILLING_INTERVAL_WIRE.yearly);
    expect(toBillingIntervalWireValue(1)).toBe(BILLING_INTERVAL_WIRE.monthly);
    expect(toBillingIntervalWireValue(undefined)).toBe(BILLING_INTERVAL_WIRE.monthly);
  });
});

describe('resolveMembershipPlanErrorKey', () => {
  it('maps the per-currency price refusals', () => {
    expect(
      resolveMembershipPlanErrorKey({
        result: { detail: 'membership.plan.stripe_price_already_used' },
      })
    ).toBe('api.membership.plan.stripe_price_already_used');
    expect(
      resolveMembershipPlanErrorKey({ result: { detail: 'currency.not_found' } })
    ).toBe('api.currency.not_found');
  });
});
