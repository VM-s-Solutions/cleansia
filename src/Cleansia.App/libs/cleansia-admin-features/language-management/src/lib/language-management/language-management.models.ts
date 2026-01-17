import { TemplateRef } from '@angular/core';
import { LanguageListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

// Map language codes to country codes for flag display
export const LANGUAGE_TO_COUNTRY_MAP: Record<string, string> = {
  en: 'us',
  cs: 'cz',
  de: 'de',
  fr: 'fr',
  es: 'es',
  it: 'it',
  pl: 'pl',
  sk: 'sk',
  uk: 'ua',
  ru: 'ru',
  pt: 'pt',
  nl: 'nl',
  ja: 'jp',
  zh: 'cn',
  ko: 'kr',
  ar: 'sa',
  he: 'il',
  tr: 'tr',
  vi: 'vn',
  th: 'th',
  sv: 'se',
  da: 'dk',
  fi: 'fi',
  no: 'no',
  hu: 'hu',
  ro: 'ro',
  bg: 'bg',
  hr: 'hr',
  sl: 'si',
  el: 'gr',
};

export function getLanguageToCountryCode(languageCode: string | undefined): string {
  if (!languageCode) return '';
  const lowerCode = languageCode.toLowerCase();
  return LANGUAGE_TO_COUNTRY_MAP[lowerCode] || lowerCode;
}

export function getLanguageTableDefinition(
  defs: {
    onEdit: (row: LanguageListItem) => void;
    onDelete: (row: LanguageListItem) => void;
  },
  translate: TranslateService,
  flagTemplate?: TemplateRef<LanguageListItem>
): { columns: TableColumn<LanguageListItem>[]; actions: TableAction<LanguageListItem>[] } {
  return {
    columns: [
      {
        id: 'flag',
        field: 'code',
        header: '',
        sortable: false,
        width: '60px',
        customTemplate: flagTemplate,
      },
      {
        id: 'code',
        field: 'code',
        header: translate.instant('pages.language_management.columns.code'),
        sortable: true,
        width: '20%',
      },
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.language_management.columns.name'),
        sortable: true,
        width: '50%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.language_management.edit_language'),
        color: 'warning',
        onClick: (row: LanguageListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.language_management.delete_language'),
        color: 'danger',
        onClick: (row: LanguageListItem) => defs.onDelete(row),
      },
    ],
  };
}