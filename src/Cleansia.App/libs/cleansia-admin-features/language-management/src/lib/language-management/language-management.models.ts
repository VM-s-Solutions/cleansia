import { LanguageListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getLanguageTableDefinition(
  defs: {
    onEdit: (row: LanguageListItem) => void;
    onDelete: (row: LanguageListItem) => void;
  },
  translate: TranslateService
): TableDefinition<LanguageListItem> {
  return {
    columns: [
      {
        id: 'code',
        headerName: translate.instant('pages.language_management.columns.code'),
        value: 'code',
        columnClass: 'width-20',
      },
      {
        id: 'name',
        headerName: translate.instant('pages.language_management.columns.name'),
        value: 'name',
        columnClass: 'width-60',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.language_management.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: LanguageListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.language_management.edit_language'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: LanguageListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            tooltip: {
              title: translate.instant('pages.language_management.delete_language'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-20',
      },
    ],
  };
}