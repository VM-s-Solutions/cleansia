import { CountryListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getCountryTableDefinition(
  defs: {
    onEdit: (row: CountryListItem) => void;
    onDelete: (row: CountryListItem) => void;
  },
  translate: TranslateService
): TableDefinition<CountryListItem> {
  return {
    columns: [
      {
        id: 'isoCode',
        headerName: translate.instant('pages.country_management.columns.iso_code'),
        value: 'isoCode',
        columnClass: 'width-20',
      },
      {
        id: 'name',
        headerName: translate.instant('pages.country_management.columns.name'),
        value: 'name',
        columnClass: 'width-60',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.country_management.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: CountryListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.country_management.edit_country'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: CountryListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            tooltip: {
              title: translate.instant('pages.country_management.delete_country'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-20',
      },
    ],
  };
}