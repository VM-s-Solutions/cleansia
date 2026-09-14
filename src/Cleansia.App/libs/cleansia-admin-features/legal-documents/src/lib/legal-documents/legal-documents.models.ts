import {
  LegalDocumentAudience,
  LegalDocumentTextSummaryDto,
  LegalDocumentType,
  LegalDocumentVersionDto,
} from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

const FALLBACK_LANGUAGE = 'en';

const AUDIENCE_LABEL_KEYS: Readonly<Record<LegalDocumentAudience, string>> = {
  [LegalDocumentAudience.Customer]: 'pages.legal_documents.audience.customer',
  [LegalDocumentAudience.Employee]: 'pages.legal_documents.audience.employee',
};

const TYPE_LABEL_KEYS: Readonly<Record<LegalDocumentType, string>> = {
  [LegalDocumentType.TermsOfService]: 'pages.legal_documents.type.terms_of_service',
  [LegalDocumentType.PrivacyPolicy]: 'pages.legal_documents.type.privacy_policy',
};

export function getAudienceLabelKey(audience: LegalDocumentAudience): string {
  return AUDIENCE_LABEL_KEYS[audience];
}

export function getTypeLabelKey(type: LegalDocumentType): string {
  return TYPE_LABEL_KEYS[type];
}

// The wire value is a date with no time, parsed at UTC midnight; a local-zone render would show
// the day before it west of Greenwich.
export function formatEffectiveDate(value: Date | undefined): string {
  if (!value) return '';
  const date = value instanceof Date ? value : new Date(value);
  return date.toLocaleDateString('en-GB', { timeZone: 'UTC' });
}

export function pickPreviewLanguage(
  texts: LegalDocumentTextSummaryDto[] | undefined,
  preferred: string | undefined
): string | null {
  const languages = (texts ?? []).map((text) => text.language).filter((l): l is string => !!l);
  if (languages.length === 0) return null;
  if (preferred && languages.includes(preferred)) return preferred;
  if (languages.includes(FALLBACK_LANGUAGE)) return FALLBACK_LANGUAGE;
  return languages[0];
}

export function getLegalVersionTableDefinition(
  defs: { onPreview: (row: LegalDocumentVersionDto) => void },
  translate: TranslateService
): { columns: TableColumn<LegalDocumentVersionDto>[]; actions: TableAction<LegalDocumentVersionDto>[] } {
  return {
    columns: [
      {
        id: 'audience',
        field: 'audience',
        header: translate.instant('pages.legal_documents.columns.audience'),
        getValue: (row: LegalDocumentVersionDto) => translate.instant(getAudienceLabelKey(row.audience)),
        width: '12%',
      },
      {
        id: 'type',
        field: 'type',
        header: translate.instant('pages.legal_documents.columns.type'),
        getValue: (row: LegalDocumentVersionDto) => translate.instant(getTypeLabelKey(row.type)),
        width: '16%',
      },
      {
        id: 'market',
        field: 'countryIsoCode',
        header: translate.instant('pages.legal_documents.columns.market'),
        getValue: (row: LegalDocumentVersionDto) =>
          row.countryIsoCode ?? translate.instant('pages.legal_documents.all_markets'),
        width: '12%',
      },
      {
        id: 'effectiveFrom',
        field: 'effectiveFrom',
        header: translate.instant('pages.legal_documents.columns.effective_from'),
        getValue: (row: LegalDocumentVersionDto) => formatEffectiveDate(row.effectiveFrom),
        width: '14%',
      },
      {
        id: 'version',
        field: 'version',
        header: translate.instant('pages.legal_documents.columns.version'),
        width: '14%',
      },
      {
        id: 'isInForce',
        field: 'isInForce',
        header: translate.instant('pages.legal_documents.columns.in_force'),
        getValue: (row: LegalDocumentVersionDto) =>
          row.isInForce ? translate.instant('global.yes') : translate.instant('global.no'),
        width: '10%',
      },
      {
        id: 'languages',
        field: 'texts',
        header: translate.instant('pages.legal_documents.columns.languages'),
        getValue: (row: LegalDocumentVersionDto) => (row.texts ?? []).map((text) => text.language).join(', '),
        width: '14%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.legal_documents.preview'),
        color: 'info',
        onClick: (row: LegalDocumentVersionDto) => defs.onPreview(row),
      },
    ],
  };
}

export function getLegalTextTableDefinition(
  defs: {
    onShow: (row: LegalDocumentTextSummaryDto) => void;
    isShown: (row: LegalDocumentTextSummaryDto) => boolean;
  },
  translate: TranslateService
): { columns: TableColumn<LegalDocumentTextSummaryDto>[]; actions: TableAction<LegalDocumentTextSummaryDto>[] } {
  return {
    columns: [
      {
        id: 'language',
        field: 'language',
        header: translate.instant('pages.legal_documents.columns.language'),
        width: '10%',
      },
      {
        id: 'title',
        field: 'title',
        header: translate.instant('pages.legal_documents.columns.title'),
        width: '30%',
      },
      {
        id: 'contentHash',
        field: 'contentHash',
        header: translate.instant('pages.legal_documents.columns.hash'),
        width: '50%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.legal_documents.show_language'),
        color: 'info',
        visible: (row: LegalDocumentTextSummaryDto) => !defs.isShown(row),
        onClick: (row: LegalDocumentTextSummaryDto) => defs.onShow(row),
      },
    ],
  };
}
