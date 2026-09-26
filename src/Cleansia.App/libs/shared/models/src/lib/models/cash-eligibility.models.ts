/**
 * Whether a booking may be paid in cash — `BookingPolicy.AllowsCash`: a signed-in customer, on a
 * booking the server says one cleaner does alone. `requiredEmployees` is the quote's figure for the
 * current selection, never a client estimate; null means no quote describes that selection yet.
 * The server re-decides on create, so this only decides what the payment step offers.
 */
export type CashEligibility =
  | { kind: 'available' }
  | { kind: 'needs_account' }
  | { kind: 'needs_card'; requiredCleaners: number }
  | { kind: 'pending' };

export function resolveCashEligibility(
  signedIn: boolean,
  requiredEmployees: number | null,
): CashEligibility {
  if (requiredEmployees !== null && requiredEmployees > 1) {
    return { kind: 'needs_card', requiredCleaners: requiredEmployees };
  }
  if (!signedIn) return { kind: 'needs_account' };
  return requiredEmployees === 1 ? { kind: 'available' } : { kind: 'pending' };
}

/** A verdict that takes cash away. An unknown crew is not one — the selection is kept until it is known. */
export function cashIsRefused(eligibility: CashEligibility): boolean {
  return eligibility.kind === 'needs_account' || eligibility.kind === 'needs_card';
}
