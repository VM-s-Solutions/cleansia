import { EmailType, EmailTypeListItemDto } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getEmailTypeTableDefinition(
  defs: {
    onViewDetail: (row: EmailTypeListItemDto) => void;
  },
  translate: TranslateService
): TableDefinition<EmailTypeListItemDto> {
  return {
    columns: [
      {
        id: 'displayName',
        headerName: translate.instant('pages.template_management.columns.email_type'),
        value: 'displayName',
        columnClass: 'width-25',
      },
      {
        id: 'translationCount',
        headerName: translate.instant('pages.template_management.columns.translation_count'),
        value: (row?: EmailTypeListItemDto) => {
          return row?.translationCount?.toString() ?? '0';
        },
        columnClass: 'width-15',
      },
      {
        id: 'availableLanguages',
        headerName: translate.instant('pages.template_management.columns.languages'),
        value: (row?: EmailTypeListItemDto) => {
          return row?.availableLanguages?.join(', ') ?? '';
        },
        columnClass: 'width-25',
      },
      {
        id: 'lastModified',
        headerName: translate.instant('pages.template_management.columns.last_modified'),
        value: (row?: EmailTypeListItemDto) => {
          if (!row?.lastModified) return '-';
          return new Date(row.lastModified.toString()).toLocaleDateString();
        },
        columnClass: 'width-15',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.template_management.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-eye',
            onClick: (row: EmailTypeListItemDto) => defs.onViewDetail(row),
            buttonPalette: 'p-button-info p-button-sm',
            tooltip: {
              title: translate.instant('pages.template_management.view_translations'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-10',
      },
    ],
  };
}
