import { WorkContractFacts } from '@cleansia/customer-services';
import { formatMoney, localeFor } from '@cleansia/utils';

export interface WorkContractFactRow {
  labelKey: string;
  value: string;
}

const FACTS_KEY = 'pages.order_contract.facts';

/** "25. 09. 2026 8:00" — a date and a time in the reader's locale. */
export function formatDateTime(date: Date, locale: string): string {
  return new Intl.DateTimeFormat(locale, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(date);
}

/** "25. 09. 2026 8:00 – 10:30": the start instant and the end the estimate produces. */
export function formatCleaningWindow(
  start: Date | undefined,
  estimatedMinutes: number,
  locale: string,
): string {
  if (!start) return '';
  const from = formatDateTime(start, locale);
  if (!estimatedMinutes) return from;
  const end = new Date(start.getTime() + estimatedMinutes * 60_000);
  const to = new Intl.DateTimeFormat(locale, { hour: 'numeric', minute: '2-digit' }).format(end);
  return `${from} – ${to}`;
}

/**
 * The job as it was frozen on the acceptance row — never the live order, which moves on after the
 * cleaner agreed. The price is the frozen figure in the frozen currency, formatted for the reader.
 */
export function buildWorkContractFactRows(
  facts: WorkContractFacts | undefined,
  lang: string,
): WorkContractFactRow[] {
  if (!facts) return [];
  const locale = localeFor(lang);
  const rows: WorkContractFactRow[] = [
    { labelKey: `${FACTS_KEY}.order_number`, value: facts.orderNumber ?? '' },
    {
      labelKey: `${FACTS_KEY}.window`,
      value: formatCleaningWindow(facts.cleaningDateTimeUtc, facts.estimatedMinutes, locale),
    },
    {
      labelKey: `${FACTS_KEY}.price`,
      value: formatMoney(facts.totalPrice, facts.currencyCode, locale),
    },
    { labelKey: `${FACTS_KEY}.location`, value: facts.locationApproximate ?? '' },
    { labelKey: `${FACTS_KEY}.rooms_bathrooms`, value: `${facts.rooms} / ${facts.bathrooms}` },
  ];
  const services = (facts.services ?? []).map((line) => line.name).filter(Boolean);
  if (services.length) rows.push({ labelKey: `${FACTS_KEY}.services`, value: services.join(', ') });
  const packages = (facts.packages ?? []).map((line) => line.name).filter(Boolean);
  if (packages.length) rows.push({ labelKey: `${FACTS_KEY}.packages`, value: packages.join(', ') });
  const extras = facts.extraSlugs ?? [];
  if (extras.length) rows.push({ labelKey: `${FACTS_KEY}.extras`, value: extras.join(', ') });
  return rows;
}

export function languageDisplayName(code: string, uiLang: string): string {
  try {
    const name = new Intl.DisplayNames([uiLang], { type: 'language' }).of(code);
    if (name && name !== code) return name.charAt(0).toUpperCase() + name.slice(1);
  } catch {
    // An unknown locale tag is answered below.
  }
  return code.toUpperCase();
}
