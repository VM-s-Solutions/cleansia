import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export interface MembershipPlanPriceEntry {
  price: number;
  stripePriceId: string;
}

export interface MembershipPlanPriceBlockValue {
  price: number | string | null;
  stripePriceId: string | null;
}

export interface PlanCurrencyOption {
  code: string;
  symbol: string;
  name: string;
  isDefault: boolean;
  isActive: boolean;
}

export const PRICE_BLOCK_HALF_FILLED_ERROR = 'priceBlockHalfFilled';

// The number text input hands the control a string, and a cleared one the empty string.
export function toPriceNumber(
  value: number | string | null | undefined
): number | null {
  if (value === null || value === undefined) return null;
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;
  if (value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function hasStripePriceId(value: string | null | undefined): boolean {
  return (value ?? '').trim().length > 0;
}

export function priceBlockCompleteValidator(): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const hasPrice =
      toPriceNumber(group.get('price')?.value as number | string | null) !==
      null;
    const hasStripeId = hasStripePriceId(
      group.get('stripePriceId')?.value as string | null
    );
    return hasPrice === hasStripeId
      ? null
      : { [PRICE_BLOCK_HALF_FILLED_ERROR]: true };
  };
}

export function collectFilledPriceBlocks(blocks: {
  [code: string]: MembershipPlanPriceBlockValue;
}): { [code: string]: MembershipPlanPriceEntry } {
  const filled: { [code: string]: MembershipPlanPriceEntry } = {};
  for (const [code, block] of Object.entries(blocks)) {
    const price = toPriceNumber(block.price);
    const stripePriceId = (block.stripePriceId ?? '').trim();
    if (price === null || stripePriceId === '') continue;
    filled[code] = { price, stripePriceId };
  }
  return filled;
}
