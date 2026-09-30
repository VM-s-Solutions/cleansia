import {
  CleanerCashHeldDto,
  RecordCashRemittanceCommand,
  WriteOffCashHeldCommand,
} from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export enum CashHeldActionKind {
  Remittance = 'remittance',
  WriteOff = 'writeOff',
}

export interface CashHeldAction {
  kind: CashHeldActionKind;
  row: CleanerCashHeldDto;
}

export interface CashHeldActionCopy {
  titleKey: string;
  ledeKey: string;
  noteKey: string;
  submitKey: string;
  successKey: string;
  icon: string;
  severity: 'primary' | 'danger';
  outlined: boolean;
  noteRequired: boolean;
}

export const CASH_HELD_ACTION_COPY: Readonly<Record<CashHeldActionKind, CashHeldActionCopy>> = {
  [CashHeldActionKind.Remittance]: {
    titleKey: 'pages.cash_held.remittance.title',
    ledeKey: 'pages.cash_held.remittance.lede',
    noteKey: 'pages.cash_held.remittance.note',
    submitKey: 'pages.cash_held.remittance.submit',
    successKey: 'pages.cash_held.messages.remittance_success',
    icon: 'pi pi-wallet',
    severity: 'primary',
    outlined: false,
    noteRequired: false,
  },
  [CashHeldActionKind.WriteOff]: {
    titleKey: 'pages.cash_held.write_off.title',
    ledeKey: 'pages.cash_held.write_off.lede',
    noteKey: 'pages.cash_held.write_off.note',
    submitKey: 'pages.cash_held.write_off.submit',
    successKey: 'pages.cash_held.messages.write_off_success',
    icon: 'pi pi-times-circle',
    severity: 'danger',
    outlined: true,
    noteRequired: true,
  },
};

export function parseCashAmount(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function formatCashHeld(row: CleanerCashHeldDto, lang: string | undefined): string {
  return formatMoney(row.amount, row.currencyCode, localeFor(lang), { fractionDigits: 2 });
}

export function buildRecordCashRemittanceCommand(
  row: CleanerCashHeldDto,
  amount: number,
  note: string
): RecordCashRemittanceCommand {
  const command = new RecordCashRemittanceCommand();
  command.employeeId = row.employeeId;
  command.currencyId = row.currencyId;
  command.amount = amount;
  command.note = note || undefined;
  return command;
}

export function buildWriteOffCashHeldCommand(
  row: CleanerCashHeldDto,
  amount: number,
  note: string
): WriteOffCashHeldCommand {
  const command = new WriteOffCashHeldCommand();
  command.employeeId = row.employeeId;
  command.currencyId = row.currencyId;
  command.amount = amount;
  command.note = note;
  return command;
}

export function getCashHeldTableDefinition(
  defs: {
    onRecordRemittance: (row: CleanerCashHeldDto) => void;
    onWriteOff: (row: CleanerCashHeldDto) => void;
    isBusy: () => boolean;
  },
  translate: TranslateService,
  permissions: PermissionService
): { columns: TableColumn<CleanerCashHeldDto>[]; actions: TableAction<CleanerCashHeldDto>[] } {
  return {
    columns: [
      {
        id: 'employeeName',
        field: 'employeeName',
        header: translate.instant('pages.cash_held.columns.cleaner'),
        getValue: (row: CleanerCashHeldDto) => row.employeeName || '-',
        width: '45%',
      },
      {
        id: 'currencyCode',
        field: 'currencyCode',
        header: translate.instant('pages.cash_held.columns.currency'),
        width: '15%',
      },
      {
        id: 'amount',
        numeric: true,
        field: 'amount',
        header: translate.instant('pages.cash_held.columns.amount'),
        getValue: (row: CleanerCashHeldDto) => formatCashHeld(row, translate.currentLang),
        width: '25%',
      },
    ],
    actions: [
      {
        icon: CASH_HELD_ACTION_COPY[CashHeldActionKind.Remittance].icon,
        tooltip: translate.instant('pages.cash_held.actions.record_remittance'),
        color: 'primary',
        visible: () => permissions.hasPolicy(Policy.CanRecordCashRemittance),
        disabled: () => defs.isBusy(),
        onClick: (row: CleanerCashHeldDto) => defs.onRecordRemittance(row),
      },
      {
        icon: CASH_HELD_ACTION_COPY[CashHeldActionKind.WriteOff].icon,
        tooltip: translate.instant('pages.cash_held.actions.write_off'),
        color: 'danger',
        visible: () => permissions.hasPolicy(Policy.CanWriteOffCashHeld),
        disabled: () => defs.isBusy(),
        onClick: (row: CleanerCashHeldDto) => defs.onWriteOff(row),
      },
    ],
  };
}
