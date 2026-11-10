import { TemplateRef } from '@angular/core';
import { EmailTypeListItemDto } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { formatDate } from '@cleansia/utils';
import { emailTypeLabel } from '../email-type-label';
import { TranslateService } from '@ngx-translate/core';

export function getEmailTypeTableDefinition(
  translate: TranslateService,
  languagesTemplate?: TemplateRef<EmailTypeListItemDto>
): { columns: TableColumn<EmailTypeListItemDto>[]; actions: TableAction<EmailTypeListItemDto>[] } {
  return {
    columns: [
      {
        id: 'displayName',
        field: 'displayName',
        header: translate.instant('pages.template_management.columns.email_type'),
        getValue: (row: EmailTypeListItemDto) => emailTypeLabel(translate, row.emailType, row.displayName),
        width: '25%',
      },
      {
        id: 'translationCount',
        numeric: true,
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
        numeric: true,
        field: 'lastModified',
        header: translate.instant('pages.template_management.columns.last_modified'),
        getValue: (row: EmailTypeListItemDto) => formatDate(row?.lastModified, translate.currentLang) || '-',
        width: '15%',
      },
    ],
    actions: [],
  };
}
