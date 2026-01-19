import { TemplateRef } from '@angular/core';
import { EmailType, EmailTypeListItemDto } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getEmailTypeTableDefinition(
  defs: {
    onViewDetail: (row: EmailTypeListItemDto) => void;
  },
  translate: TranslateService,
  languagesTemplate?: TemplateRef<EmailTypeListItemDto>
): { columns: TableColumn<EmailTypeListItemDto>[]; actions: TableAction<EmailTypeListItemDto>[] } {
  return {
    columns: [
      {
        id: 'displayName',
        field: 'displayName',
        header: translate.instant('pages.template_management.columns.email_type'),
        width: '25%',
      },
      {
        id: 'translationCount',
        field: 'translationCount',
        header: translate.instant('pages.template_management.columns.translation_count'),
        getValue: (row: EmailTypeListItemDto) => {
          return row?.translationCount?.toString() ?? '0';
        },
        width: '15%',
      },
      {
        id: 'availableLanguages',
        field: 'availableLanguages',
        header: translate.instant('pages.template_management.columns.languages'),
        customTemplate: languagesTemplate,
        width: '25%',
      },
      {
        id: 'lastModified',
        field: 'lastModified',
        header: translate.instant('pages.template_management.columns.last_modified'),
        getValue: (row: EmailTypeListItemDto) => {
          if (!row?.lastModified) return '-';
          return new Date(row.lastModified.toString()).toLocaleDateString();
        },
        width: '15%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.template_management.view_translations'),
        color: 'info',
        onClick: (row: EmailTypeListItemDto) => defs.onViewDetail(row),
      },
    ],
  };
}
