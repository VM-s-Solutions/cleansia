import { TemplateRef } from '@angular/core';
import { AdminReferralListItem, ReferralStatus } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

/** A credit amount the way the admin tables print money: in its currency, two decimals. */
export function formatAdminCredit(value: number | undefined, currencyCode: string | undefined, lang: string): string {
  return formatMoney(value ?? 0, currencyCode, localeFor(lang), { fractionDigits: 2 });
}

/** Referrer / referred, as credited on qualification; a dash until either side was paid. */
export function formatReferralCredit(row: AdminReferralListItem, translate: TranslateService): string {
  if (row.creditAwardedToReferrer == null && row.creditAwardedToReferred == null) {
    return translate.instant('pages.loyalty_referrals.not_yet');
  }
  return translate.instant('pages.loyalty_referrals.credit_format', {
    referrer: formatAdminCredit(row.creditAwardedToReferrer, row.creditCurrencyCode, translate.currentLang),
    referred: formatAdminCredit(row.creditAwardedToReferred, row.creditCurrencyCode, translate.currentLang),
  });
}

export function getReferralTableColumns(
  translate: TranslateService,
  statusTemplate?: TemplateRef<AdminReferralListItem>
): TableColumn<AdminReferralListItem>[] {
  const day = (value?: Date) =>
    formatDate(value, translate.currentLang) || translate.instant('pages.loyalty_referrals.not_yet');
  return [
    {
      id: 'referrer',
      field: 'referrerEmail',
      header: translate.instant('pages.loyalty_referrals.column.referrer'),
      getValue: (row) => row.referrerEmail || '—',
      width: '20%',
    },
    {
      id: 'referred',
      field: 'referredEmail',
      header: translate.instant('pages.loyalty_referrals.column.referred'),
      getValue: (row) => row.referredEmail || '—',
      width: '20%',
    },
    {
      id: 'status',
      field: 'status',
      header: translate.instant('pages.loyalty_referrals.column.status'),
      align: 'center',
      customTemplate: statusTemplate,
      width: '12%',
    },
    {
      id: 'acceptedOn',
      field: 'acceptedOn',
      header: translate.instant('pages.loyalty_referrals.column.accepted_on'),
      numeric: true,
      getValue: (row) => day(row.acceptedOn),
      width: '12%',
    },
    {
      id: 'qualifiedOn',
      field: 'firstQualifyingOrderOn',
      header: translate.instant('pages.loyalty_referrals.column.qualified_on'),
      numeric: true,
      getValue: (row) => day(row.firstQualifyingOrderOn),
      width: '12%',
    },
    {
      id: 'creditAwarded',
      field: 'creditAwardedToReferrer',
      header: translate.instant('pages.loyalty_referrals.column.credit_awarded'),
      numeric: true,
      getValue: (row) => formatReferralCredit(row, translate),
      width: '14%',
    },
  ];
}

export function getReferralInterventionActions(
  defs: {
    canIntervene: boolean;
    onReverse: (row: AdminReferralListItem) => void;
    onForceQualify: (row: AdminReferralListItem) => void;
  },
  translate: TranslateService
): TableAction<AdminReferralListItem>[] {
  if (!defs.canIntervene) return [];
  return [
    {
      icon: 'pi pi-undo',
      tooltip: translate.instant('pages.loyalty_referrals.actions.reverse'),
      color: 'danger',
      visible: (row) => row.status === ReferralStatus.Qualified,
      onClick: (row) => defs.onReverse(row),
    },
    {
      icon: 'pi pi-check-circle',
      tooltip: translate.instant(
        'pages.loyalty_referrals.actions.force_qualify'
      ),
      color: 'success',
      visible: (row) => row.status === ReferralStatus.Accepted,
      onClick: (row) => defs.onForceQualify(row),
    },
  ];
}

/**
 * Backend BusinessErrorMessage code -> i18n key, explicit because the
 * intervention path is money-adjacent (mirrors the disputes-management map).
 */

