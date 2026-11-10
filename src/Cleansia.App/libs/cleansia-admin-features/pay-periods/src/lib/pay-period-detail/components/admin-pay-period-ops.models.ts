export type AdminPayPeriodOpsPanel = 'markPaid' | 'closeNow' | 'changeEnd';

// A period's dates arrive as UTC midnights, while the calendar and the client's date serialiser work in local days.
export function localDay(utcDate: Date, plusDays = 0): Date {
  return new Date(utcDate.getUTCFullYear(), utcDate.getUTCMonth(), utcDate.getUTCDate() + plusDays);
}
