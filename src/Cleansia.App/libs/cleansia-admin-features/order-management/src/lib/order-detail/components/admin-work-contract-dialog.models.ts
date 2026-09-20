import { WorkContractDto, WorkContractFacts } from '@cleansia/admin-services';
import { formatMoney, localeFor } from '@cleansia/utils';

export interface AdminWorkContractDialogData {
  acceptanceId: string;
}

export interface WorkContractRow {
  labelKey: string;
  value: string;
  valueKey?: string;
}

const DIALOG_KEY = 'pages.order_detail.work_contract.dialog';
const FACTS_KEY = `${DIALOG_KEY}.facts`;
const ACCEPTANCE_KEY = `${DIALOG_KEY}.acceptance`;

export const WORK_CONTRACT_HASH_ROW = `${ACCEPTANCE_KEY}.hash`;
const HASH_UNAVAILABLE_KEY = `${ACCEPTANCE_KEY}.hash_unavailable`;

export function formatDateTime(date: Date | undefined): string {
  return date ? date.toLocaleString('en-GB') : '';
}

export function formatCleaningWindow(start: Date | undefined, estimatedMinutes: number): string {
  if (!start) return '';
  const from = start.toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' });
  if (!estimatedMinutes) return from;
  const end = new Date(start.getTime() + estimatedMinutes * 60_000);
  return `${from} – ${end.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}`;
}

export function buildWorkContractFactRows(
  facts: WorkContractFacts | undefined,
  lang: string
): WorkContractRow[] {
  if (!facts) return [];
  const rows: WorkContractRow[] = [
    { labelKey: `${FACTS_KEY}.order_number`, value: facts.orderNumber ?? '' },
    {
      labelKey: `${FACTS_KEY}.window`,
      value: formatCleaningWindow(facts.cleaningDateTimeUtc, facts.estimatedMinutes),
    },
    {
      labelKey: `${FACTS_KEY}.price`,
      value: formatMoney(facts.totalPrice, facts.currencyCode, localeFor(lang)),
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

export function buildWorkContractAcceptanceRows(
  contract: WorkContractDto | null,
  acceptedTextHash: string | null,
  lang: string
): WorkContractRow[] {
  const acceptance = contract?.acceptance;
  if (!acceptance) return [];
  const hashRow: WorkContractRow = acceptedTextHash
    ? { labelKey: WORK_CONTRACT_HASH_ROW, value: acceptedTextHash }
    : { labelKey: WORK_CONTRACT_HASH_ROW, value: '', valueKey: HASH_UNAVAILABLE_KEY };
  return [
    { labelKey: `${ACCEPTANCE_KEY}.accepted_on`, value: formatDateTime(acceptance.acceptedOn) },
    { labelKey: `${ACCEPTANCE_KEY}.version`, value: acceptance.documentVersion ?? '' },
    {
      labelKey: `${ACCEPTANCE_KEY}.language`,
      value: languageDisplayName(acceptance.acceptedLanguage ?? '', lang),
    },
    hashRow,
  ];
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
