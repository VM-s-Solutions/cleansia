import { GetMyServingCleanersResponse } from '../client/customer-client';

/**
 * Structurally an `ICleansiaSelectOption`, declared here so this lib keeps no dependency on the
 * shared component library.
 */
export interface PreferredCleanerOption {
  label: string;
  value: string;
  disabled: boolean;
}

/**
 * Who a customer may ask for, rendered from `GET /api/Order/MyServingCleaners` — the one source both
 * mobile clients read and the only one the booking wizard and the order detail may read either, so
 * the two web surfaces cannot answer "who is offerable" differently.
 *
 * `isAvailableForRequestedSlot` is a tri-state (ADR-0039 D5): `false` is the only value that means
 * "not for this booking". `null`/`undefined` means the question was not answered — no slot in the
 * request, a selection the platform would not book, a non-member, or a check that could not run — and
 * renders as an ordinary selectable row.
 *
 * An unavailable cleaner is SHOWN and unselectable, never removed (ADR-0039 D7.1), and the line that
 * marks them names no reason: it is a statement about what Cleansia can offer, which stays true if
 * the predicate later widens, rather than about what the person is doing, which would not.
 */
export function toPreferredCleanerOptions(
  cleaners: readonly GetMyServingCleanersResponse[],
  unavailableLabel: string
): PreferredCleanerOption[] {
  const options: PreferredCleanerOption[] = [];

  for (const cleaner of cleaners) {
    const employeeId = cleaner.employeeId?.trim();
    const fullName = cleaner.fullName?.trim();
    if (!employeeId || !fullName) {
      continue;
    }

    const disabled = cleaner.isAvailableForRequestedSlot === false;
    options.push({
      label: disabled ? `${fullName} · ${unavailableLabel}` : fullName,
      value: employeeId,
      disabled,
    });
  }

  return options;
}

/**
 * The selection that survives a fresh roster. A cleaner the chosen slot no longer admits is cleared
 * silently — the marked row is the disclosure, and a toast about a named person's day would not be
 * (ADR-0039 D7.1).
 */
export function survivingPreferredSelection(
  cleaners: readonly GetMyServingCleanersResponse[],
  selectedEmployeeId: string | null
): string | null {
  if (!selectedEmployeeId) {
    return null;
  }

  const selected = cleaners.find(
    (cleaner) => cleaner.employeeId === selectedEmployeeId
  );

  return selected && selected.isAvailableForRequestedSlot !== false
    ? selectedEmployeeId
    : null;
}
