import {
  AcceptWorkContractCommand,
  TakeOrderCommand,
  WorkContractFacts,
} from '@cleansia/partner-services';
import { formatMoney, localeFor } from '@cleansia/utils';
import { formatDateTime } from '../../order-details/order-details.helpers';

export enum WorkContractDialogMode {
  Take = 'take',
  Accept = 'accept',
  Read = 'read',
}

export enum WorkContractDialogOutcome {
  Accepted = 'accepted',
  Refused = 'refused',
}

export type WorkContractDialogData =
  | { mode: WorkContractDialogMode.Take; orderId: string }
  | { mode: WorkContractDialogMode.Accept; orderId: string }
  | { mode: WorkContractDialogMode.Read; acceptanceId: string };

export interface WorkContractDialogResult {
  outcome: WorkContractDialogOutcome;
}

export interface WorkContractFactRow {
  labelKey: string;
  value: string;
}

const FACTS_KEY = 'pages.orders.work_contract.facts';

export function formatCleaningWindow(start: Date | undefined, estimatedMinutes: number): string {
  if (!start) return '';
  const from = formatDateTime(start);
  if (!estimatedMinutes) return from;
  const end = new Date(start.getTime() + estimatedMinutes * 60_000);
  const hours = end.getHours().toString().padStart(2, '0');
  const minutes = end.getMinutes().toString().padStart(2, '0');
  return `${from} – ${hours}:${minutes}`;
}

export function buildWorkContractFactRows(
  facts: WorkContractFacts | undefined,
  lang: string
): WorkContractFactRow[] {
  if (!facts) return [];
  const rows: WorkContractFactRow[] = [
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

export function languageDisplayName(code: string, uiLang: string): string {
  try {
    const name = new Intl.DisplayNames([uiLang], { type: 'language' }).of(code);
    if (name && name !== code) return name.charAt(0).toUpperCase() + name.slice(1);
  } catch {
    // An unknown locale tag is answered below.
  }
  return code.toUpperCase();
}

export function buildTakeOrderCommand(orderId: string, acceptedWorkContractTextId: string): TakeOrderCommand {
  const command = new TakeOrderCommand();
  command.orderId = orderId;
  command.acceptedWorkContractTextId = acceptedWorkContractTextId;
  return command;
}

export function buildAcceptWorkContractCommand(
  orderId: string,
  acceptedWorkContractTextId: string
): AcceptWorkContractCommand {
  const command = new AcceptWorkContractCommand();
  command.orderId = orderId;
  command.acceptedWorkContractTextId = acceptedWorkContractTextId;
  return command;
}
