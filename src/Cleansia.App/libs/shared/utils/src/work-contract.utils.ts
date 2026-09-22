import { formatMoney, localeFor } from './money-formatters.utils';

export interface WorkContractFactRow {
  labelKey: string;
  value: string;
}

/**
 * The frozen job facts as every generated client's `WorkContractFacts` carries them. A shared lib
 * cannot import a generated client, so the shape is named structurally here and the three DTO
 * classes satisfy it as they are.
 */
export interface WorkContractFactsLineView {
  id?: string;
  name?: string;
}

export interface WorkContractFactsView {
  orderNumber?: string;
  cleaningDateTimeUtc?: Date;
  estimatedMinutes: number;
  totalPrice: number;
  currencyCode?: string;
  locationApproximate?: string;
  rooms: number;
  bathrooms: number;
  services?: readonly WorkContractFactsLineView[];
  packages?: readonly WorkContractFactsLineView[];
  extraSlugs?: readonly string[];
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}

function formatStamp(date: Date): string {
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** "03.10.2026 10:30 – 13:00": the start instant and the end the estimate produces. */
export function formatCleaningWindow(start: Date | undefined, estimatedMinutes: number): string {
  if (!start) return '';
  const from = formatStamp(start);
  if (!estimatedMinutes) return from;
  const end = new Date(start.getTime() + estimatedMinutes * 60_000);
  return `${from} – ${pad(end.getHours())}:${pad(end.getMinutes())}`;
}

/**
 * The job as it was frozen on the acceptance row — never the live order, which moves on after the
 * cleaner agreed. The price is the frozen figure in the frozen currency, formatted for the reader;
 * `factsKey` is the caller's label namespace.
 */
export function buildWorkContractFactRows(
  facts: WorkContractFactsView | undefined,
  lang: string,
  factsKey: string
): WorkContractFactRow[] {
  if (!facts) return [];
  const rows: WorkContractFactRow[] = [
    { labelKey: `${factsKey}.order_number`, value: facts.orderNumber ?? '' },
    {
      labelKey: `${factsKey}.window`,
      value: formatCleaningWindow(facts.cleaningDateTimeUtc, facts.estimatedMinutes),
    },
    {
      labelKey: `${factsKey}.price`,
      value: formatMoney(facts.totalPrice, facts.currencyCode, localeFor(lang)),
    },
    { labelKey: `${factsKey}.location`, value: facts.locationApproximate ?? '' },
    { labelKey: `${factsKey}.rooms_bathrooms`, value: `${facts.rooms} / ${facts.bathrooms}` },
  ];
  const services = (facts.services ?? []).map((line) => line.name).filter(Boolean);
  if (services.length) rows.push({ labelKey: `${factsKey}.services`, value: services.join(', ') });
  const packages = (facts.packages ?? []).map((line) => line.name).filter(Boolean);
  if (packages.length) rows.push({ labelKey: `${factsKey}.packages`, value: packages.join(', ') });
  const extras = facts.extraSlugs ?? [];
  if (extras.length) rows.push({ labelKey: `${factsKey}.extras`, value: extras.join(', ') });
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
