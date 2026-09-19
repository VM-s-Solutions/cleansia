const formatters = new Map<string, Intl.NumberFormat>();

export interface FormatMoneyOptions {
  /** Prints exactly this many fraction digits on every amount, whole or not. */
  fractionDigits?: number;
}

function fractionOptions(digits: number | undefined, whole: boolean): Intl.NumberFormatOptions {
  if (digits !== undefined) {
    return { minimumFractionDigits: digits, maximumFractionDigits: digits };
  }
  return whole ? { minimumFractionDigits: 0, maximumFractionDigits: 0 } : {};
}

function formatterFor(
  locale: string,
  currencyCode: string | null | undefined,
  digits: number | undefined,
  whole: boolean,
): Intl.NumberFormat {
  const key = `${locale}|${currencyCode ?? ''}|${digits ?? (whole ? 'w' : 'f')}`;
  let formatter = formatters.get(key);
  if (!formatter) {
    const fraction = fractionOptions(digits, whole);
    formatter = currencyCode
      ? new Intl.NumberFormat(locale, { style: 'currency', currency: currencyCode, ...fraction })
      : new Intl.NumberFormat(locale, fraction);
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
 * is not charged. The admin and partner tables pin `fractionDigits` instead, so a column of amounts
 * lines up on the decimal point. Without a currency code the bare number prints — the catalogue is
 * priced in the platform default, which arrives one request after the prices do, and
 * `Intl.NumberFormat` throws on an empty currency rather than degrading.
 */
export function formatMoney(
  value: number,
  currencyCode: string | null | undefined,
  locale: string,
  options: FormatMoneyOptions = {},
): string {
  const amount = Number(value) || 0;
  const rounded = Math.round(amount) || 0;
  const isWhole = Math.abs(amount - rounded) < 0.005;
  return formatterFor(locale, currencyCode, options.fractionDigits, isWhole).format(isWhole ? rounded : amount);
}

const LOCALE_BY_LANGUAGE: Record<string, string> = {
  en: 'en-US',
  cs: 'cs-CZ',
  sk: 'sk-SK',
  uk: 'uk-UA',
  ru: 'ru-RU',
};

/** The `Intl` locale an amount is formatted in for one of the five app languages. */
export function localeFor(lang: string | undefined): string {
  return (lang && LOCALE_BY_LANGUAGE[lang]) || 'en-US';
}
