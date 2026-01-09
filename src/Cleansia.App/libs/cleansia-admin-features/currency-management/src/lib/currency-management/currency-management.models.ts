import { CurrencyListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getCurrencyTableDefinition(
  defs: {
    onEdit: (row: CurrencyListItem) => void;
    onDelete: (row: CurrencyListItem) => void;
  },
  translate: TranslateService
): TableDefinition<CurrencyListItem> {
  return {
    columns: [
      {
        id: 'code',
        headerName: translate.instant('pages.currency_management.columns.code'),
        value: 'code',
        columnClass: 'width-15',
      },
      {
        id: 'symbol',
        headerName: translate.instant('pages.currency_management.columns.symbol'),
        value: 'symbol',
        columnClass: 'width-10',
      },
      {
        id: 'name',
        headerName: translate.instant('pages.currency_management.columns.name'),
        value: 'name',
        columnClass: 'width-30',
      },
      {
        id: 'exchangeRate',
        headerName: translate.instant('pages.currency_management.columns.exchange_rate'),
        value: 'exchangeRate',
        columnClass: 'width-15',
      },
      {
        id: 'isDefault',
        headerName: translate.instant('pages.currency_management.columns.is_default'),
        value: (row?: CurrencyListItem) =>
          row && (row as any).isDefault
            ? translate.instant('global.yes')
            : translate.instant('global.no'),
        columnClass: 'width-10',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.currency_management.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: CurrencyListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.currency_management.edit_currency'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: CurrencyListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            disabled: (row: CurrencyListItem) => (row as any).isDefault,
            tooltip: {
              title: translate.instant('pages.currency_management.delete_currency'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-20',
      },
    ],
  };
}