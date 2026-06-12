import {
  ConsentType,
  GdprRequestDto,
  GdprRequestStatus,
  UserConsentDto,
} from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export const GDPR_REQUEST_STATUS_LABEL_KEYS: Readonly<
  Record<GdprRequestStatus, string>
> = {
  [GdprRequestStatus.Pending]: 'pages.data_protection.request_status.Pending',
  [GdprRequestStatus.Processing]:
    'pages.data_protection.request_status.Processing',
  [GdprRequestStatus.Completed]:
    'pages.data_protection.request_status.Completed',
  [GdprRequestStatus.Failed]: 'pages.data_protection.request_status.Failed',
};

export const CONSENT_TYPE_LABEL_KEYS: Readonly<Record<ConsentType, string>> = {
  [ConsentType.TermsOfService]:
    'pages.data_protection.consent_types.terms_of_service',
  [ConsentType.PrivacyPolicy]:
    'pages.data_protection.consent_types.privacy_policy',
  [ConsentType.MarketingEmails]:
    'pages.data_protection.consent_types.marketing_emails',
  [ConsentType.DataProcessing]:
    'pages.data_protection.consent_types.data_processing',
};

const REQUEST_TYPE_LABEL_KEYS: Readonly<Record<string, string>> = {
  Export: 'pages.data_protection.request_types.export',
  Deletion: 'pages.data_protection.request_types.deletion',
};

export function getGdprRequestTableDefinition(
  translate: TranslateService,
  formatDate: (d?: Date) => string
): {
  columns: TableColumn<GdprRequestDto>[];
  actions: TableAction<GdprRequestDto>[];
} {
  return {
    columns: [
      {
        id: 'userId',
        field: 'userId',
        header: translate.instant('pages.data_protection.requests.columns.user_id'),
        getValue: (row) => row.userId ?? '—',
        width: '20%',
      },
      {
        id: 'requestType',
        field: 'requestType',
        header: translate.instant('pages.data_protection.requests.columns.type'),
        getValue: (row) => {
          const key = row.requestType
            ? REQUEST_TYPE_LABEL_KEYS[row.requestType]
            : undefined;
          return key ? translate.instant(key) : row.requestType ?? '—';
        },
        width: '10%',
      },
      {
        id: 'status',
        field: 'status',
        header: translate.instant('pages.data_protection.requests.columns.status'),
        getValue: (row) =>
          row.status != null
            ? translate.instant(GDPR_REQUEST_STATUS_LABEL_KEYS[row.status])
            : '—',
        width: '11%',
      },
      {
        id: 'processedBy',
        field: 'processedBy',
        header: translate.instant(
          'pages.data_protection.requests.columns.processed_by'
        ),
        getValue: (row) => row.processedBy ?? '—',
        width: '16%',
      },
      {
        id: 'completedAt',
        field: 'completedAt',
        header: translate.instant(
          'pages.data_protection.requests.columns.completed_at'
        ),
        getValue: (row) => formatDate(row.completedAt),
        width: '13%',
      },
      {
        id: 'notes',
        field: 'notes',
        header: translate.instant('pages.data_protection.requests.columns.notes'),
        getValue: (row) => row.notes ?? '—',
        width: '17%',
      },
      {
        id: 'createdOn',
        field: 'createdOn',
        header: translate.instant(
          'pages.data_protection.requests.columns.created_on'
        ),
        getValue: (row) => formatDate(row.createdOn),
        width: '13%',
      },
    ],
    actions: [],
  };
}

export function getConsentTableDefinition(
  translate: TranslateService,
  formatDate: (d?: Date) => string
): { columns: TableColumn<UserConsentDto>[] } {
  return {
    columns: [
      {
        id: 'consentType',
        field: 'consentType',
        header: translate.instant('pages.data_protection.consents.columns.type'),
        getValue: (row) =>
          row.consentType != null
            ? translate.instant(CONSENT_TYPE_LABEL_KEYS[row.consentType])
            : '—',
        width: '24%',
      },
      {
        id: 'isGranted',
        field: 'isGranted',
        header: translate.instant('pages.data_protection.consents.columns.state'),
        getValue: (row) =>
          translate.instant(
            row.isGranted
              ? 'pages.data_protection.consents.granted'
              : 'pages.data_protection.consents.withdrawn'
          ),
        width: '16%',
      },
      {
        id: 'grantedAt',
        field: 'grantedAt',
        header: translate.instant(
          'pages.data_protection.consents.columns.granted_at'
        ),
        getValue: (row) => formatDate(row.grantedAt),
        width: '20%',
      },
      {
        id: 'withdrawnAt',
        field: 'withdrawnAt',
        header: translate.instant(
          'pages.data_protection.consents.columns.withdrawn_at'
        ),
        getValue: (row) => formatDate(row.withdrawnAt),
        width: '20%',
      },
      {
        id: 'createdOn',
        field: 'createdOn',
        header: translate.instant(
          'pages.data_protection.consents.columns.created_on'
        ),
        getValue: (row) => formatDate(row.createdOn),
        width: '20%',
      },
    ],
  };
}
