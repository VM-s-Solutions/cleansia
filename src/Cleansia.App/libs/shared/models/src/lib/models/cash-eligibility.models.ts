/**
 * Cash needs a signed-in customer, one quoted cleaner and a verified usable card in the quoted currency.
 * Null inputs mean the selection or cards have not been checked; the server rechecks on create.
 */
export type CashEligibility =
  | { kind: 'available' }
  | { kind: 'needs_account' }
  | { kind: 'needs_card'; requiredCleaners: number }
  | { kind: 'needs_saved_card' }
  | { kind: 'pending' };

export function resolveCashEligibility(
  signedIn: boolean,
  requiredEmployees: number | null,
  hasUsableSavedCard: boolean | null,
): CashEligibility {
  if (requiredEmployees !== null && requiredEmployees > 1) {
    return { kind: 'needs_card', requiredCleaners: requiredEmployees };
  }
  if (!signedIn) return { kind: 'needs_account' };
  if (requiredEmployees !== 1 || hasUsableSavedCard === null) return { kind: 'pending' };
  return hasUsableSavedCard ? { kind: 'available' } : { kind: 'needs_saved_card' };
}

/** Only a refusal clears cash; pending checks keep the selection while confirmation stays disabled. */
export function cashIsRefused(eligibility: CashEligibility): boolean {
  return eligibility.kind === 'needs_account' || eligibility.kind === 'needs_card' || eligibility.kind === 'needs_saved_card';
}

export function hasUsableCashCard(
  cards: readonly { id: string | undefined; currencyCode: string | undefined; expMonth: number; expYear: number }[] | null,
  currencyCode: string | null,
  now = new Date(),
): boolean | null {
  if (cards === null || !currencyCode) return null;
  return cards.some((card) =>
    !!card.id?.trim() &&
    card.currencyCode?.toUpperCase() === currencyCode.toUpperCase() &&
    Number.isInteger(card.expYear) && Number.isInteger(card.expMonth) &&
    card.expMonth >= 1 && card.expMonth <= 12 &&
    (card.expYear > now.getUTCFullYear() ||
      (card.expYear === now.getUTCFullYear() && card.expMonth >= now.getUTCMonth() + 1)),
  );
}
