const formatters = new Map<string, Intl.NumberFormat>();

function formatterFor(locale: string, currencyCode: string | null | undefined, whole: boolean): Intl.NumberFormat {
  const key = `${locale}|${currencyCode ?? ''}|${whole ? 'w' : 'f'}`;
  let formatter = formatters.get(key);
  if (!formatter) {
    const fractionDigits = whole ? { minimumFractionDigits: 0, maximumFractionDigits: 0 } : {};
    formatter = currencyCode
      ? new Intl.NumberFormat(locale, { style: 'currency', currency: currencyCode, ...fractionDigits })
      : new Intl.NumberFormat(locale, fractionDigits);
    formatters.set(key, formatter);
  }
  return formatter;
}

/**
 * A money amount labelled with the currency it arrived in.
 *
 * Whole amounts print without a fraction ("1 200 Kč", "€12") and fractional ones to the currency's
 * own minor unit ("57,60 Kč", "€12.50"): catalogue prices are whole and a "199,00" headline reads as
 * a form field, while a discount that produced haleře must not be rounded to a number the customer
 * is not charged. Without a currency code the bare number prints — the catalogue is priced in the
 * platform default, which arrives one request after the prices do, and `Intl.NumberFormat` throws
 * on an empty currency rather than degrading.
 */
export function formatMoney(value: number, currencyCode: string | null | undefined, locale: string): string {
  const amount = Number(value) || 0;
  const rounded = Math.round(amount) || 0;
  const isWhole = Math.abs(amount - rounded) < 0.005;
  return formatterFor(locale, currencyCode, isWhole).format(isWhole ? rounded : amount);
}
