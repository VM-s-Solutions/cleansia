import {
  Code,
  ReceivableKind,
  ReceivableListItem,
  ReceivableStatus,
  WriteOffReceivableCommand,
} from '@cleansia/admin-services';
import { ICleansiaSelectOption, TableAction, TableColumn } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export const RECEIVABLE_STATUS_LABEL_KEYS: Readonly<Record<ReceivableStatus, string>> = {
  [ReceivableStatus.Open]: 'enums.receivable_status.open',
  [ReceivableStatus.Paid]: 'enums.receivable_status.paid',
  [ReceivableStatus.WrittenOff]: 'enums.receivable_status.written_off',
};

export const RECEIVABLE_KIND_LABEL_KEYS: Readonly<Record<ReceivableKind, string>> = {
  [ReceivableKind.CashCancellationFee]: 'enums.receivable_kind.cash_cancellation_fee',
  [ReceivableKind.Lockout]: 'enums.receivable_kind.lockout',
  [ReceivableKind.UnpaidCash]: 'enums.receivable_kind.unpaid_cash',
  [ReceivableKind.TopUp]: 'enums.receivable_kind.top_up',
};

function codeLabel(
  keys: Readonly<Record<number, string>>,
  code: Code | undefined,
  translate: TranslateService
): string {
  const key = code ? keys[code.value] : undefined;
  return key ? translate.instant(key) : (code?.name ?? '-');
}

export function receivableKindLabel(row: ReceivableListItem, translate: TranslateService): string {
  return codeLabel(RECEIVABLE_KIND_LABEL_KEYS, row.kind, translate);
}

export function receivableStatusLabel(row: ReceivableListItem, translate: TranslateService): string {
  return codeLabel(RECEIVABLE_STATUS_LABEL_KEYS, row.status, translate);
}

export function formatReceivableAmount(row: ReceivableListItem, lang: string | undefined): string {
  return formatMoney(row.amount, row.currencyCode, localeFor(lang), { fractionDigits: 2 });
}

export function isOpenReceivable(row: ReceivableListItem): boolean {
  return row.status?.value === ReceivableStatus.Open;
}

export function buildReceivableStatusOptions(translate: TranslateService): ICleansiaSelectOption[] {
  return [ReceivableStatus.Open, ReceivableStatus.Paid, ReceivableStatus.WrittenOff].map((value) => ({
    label: translate.instant(RECEIVABLE_STATUS_LABEL_KEYS[value]),
    value,
  }));
}

export function buildWriteOffReceivableCommand(receivableId: string, note: string): WriteOffReceivableCommand {
  const command = new WriteOffReceivableCommand();
  command.receivableId = receivableId;
  command.note = note;
  return command;
}

export function getReceivablesTableDefinition(
  defs: {
    onViewOrder: (row: ReceivableListItem) => void;
    onWriteOff: (row: ReceivableListItem) => void;
    isBusy: () => boolean;
  },
  translate: TranslateService,
  permissions: PermissionService
): { columns: TableColumn<ReceivableListItem>[]; actions: TableAction<ReceivableListItem>[] } {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        field: 'displayOrderNumber',
        header: translate.instant('pages.receivables.columns.order'),
        width: '11%',
      },
      {
        id: 'kind',
        field: 'kind',
        header: translate.instant('pages.receivables.columns.kind'),
        sortable: true,
        getValue: (row: ReceivableListItem) => receivableKindLabel(row, translate),
        width: '16%',
      },
      {
        id: 'amount',
        numeric: true,
        field: 'amount',
        header: translate.instant('pages.receivables.columns.amount'),
        sortable: true,
        getValue: (row: ReceivableListItem) => formatReceivableAmount(row, translate.currentLang),
        width: '11%',
      },
      {
        id: 'status',
        field: 'status',
        header: translate.instant('pages.receivables.columns.status'),
        sortable: true,
        getValue: (row: ReceivableListItem) => receivableStatusLabel(row, translate),
        width: '10%',
      },
      {
        id: 'attempts',
        numeric: true,
        field: 'attempts',
        header: translate.instant('pages.receivables.columns.attempts'),
        width: '9%',
      },
      {
        id: 'createdOn',
        numeric: true,
        field: 'createdOn',
        header: translate.instant('pages.receivables.columns.created_on'),
        sortable: true,
        getValue: (row: ReceivableListItem) => formatDate(row.createdOn, translate.currentLang, 'dateTime'),
        width: '13%',
      },
      {
        id: 'writtenOffOn',
        numeric: true,
        field: 'writtenOffOn',
        header: translate.instant('pages.receivables.columns.written_off_on'),
        getValue: (row: ReceivableListItem) =>
          formatDate(row.writtenOffOn, translate.currentLang, 'dateTime') || '-',
        width: '13%',
      },
      {
        id: 'writeOffNote',
        field: 'writeOffNote',
        header: translate.instant('pages.receivables.columns.write_off_note'),
        getValue: (row: ReceivableListItem) => row.writeOffNote || '-',
        width: '17%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.receivables.actions.view_order'),
        color: 'info',
        visible: (row: ReceivableListItem) => !!row.orderId,
        onClick: (row: ReceivableListItem) => defs.onViewOrder(row),
      },
      {
        icon: 'pi pi-times-circle',
        tooltip: translate.instant('pages.receivables.actions.write_off'),
        color: 'danger',
        visible: (row: ReceivableListItem) =>
          isOpenReceivable(row) && permissions.hasPolicy(Policy.CanWriteOffReceivable),
        disabled: () => defs.isBusy(),
        onClick: (row: ReceivableListItem) => defs.onWriteOff(row),
      },
    ],
  };
}
