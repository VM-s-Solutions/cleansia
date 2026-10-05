import { TemplateRef } from '@angular/core';
import { AdminReferralListItem, ReferralStatus } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

/** A credit amount the way the admin tables print money: in its currency, two decimals. */
export function formatAdminCredit(value: number | undefined, currencyCode: string | undefined, lang: string): string {
  return formatMoney(value ?? 0, currencyCode, localeFor(lang), { fractionDigits: 2 });
}

/** Referrer / referred, each in the currency it was paid in; a dash for a side that was paid nothing. */
export function formatReferralCredit(row: AdminReferralListItem, translate: TranslateService): string {
  const notYet = translate.instant('pages.loyalty_referrals.not_yet');
  if (row.creditAwardedToReferrer == null && row.creditAwardedToReferred == null) {
    return notYet;
  }
  const side = (value: number | undefined, currencyCode: string | undefined) =>
    currencyCode ? formatAdminCredit(value, currencyCode, translate.currentLang) : notYet;
  return translate.instant('pages.loyalty_referrals.credit_format', {
    referrer: side(row.creditAwardedToReferrer, row.referrerCreditCurrencyCode),
    referred: side(row.creditAwardedToReferred, row.referredCreditCurrencyCode),
  });
}

export function isHeldReferral(row: AdminReferralListItem): boolean {
  return row.status === ReferralStatus.Accepted && !!row.holdReasons;
}

/** The translated reasons a held referral waits for an administrator on; null when it is not held. */
export function formatHoldReasons(row: AdminReferralListItem, translate: TranslateService): string | null {
  if (!isHeldReferral(row)) return null;
  return (row.holdReasons ?? '')
    .split(',')
    .map((slug) => slug.trim())
    .filter((slug) => slug.length > 0)
    .map((slug) => translate.instant(`pages.loyalty_referrals.hold_reason.${slug}`))
    .join(', ');
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
    onRelease: (row: AdminReferralListItem) => void;
    onReject: (row: AdminReferralListItem) => void;
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
      tooltip: translate.instant('pages.loyalty_referrals.actions.force_qualify'),
      color: 'success',
      visible: (row) => row.status === ReferralStatus.Accepted && !isHeldReferral(row),
      onClick: (row) => defs.onForceQualify(row),
    },
    {
      icon: 'pi pi-check-circle',
      tooltip: translate.instant('pages.loyalty_referrals.actions.release'),
      color: 'success',
      visible: (row) => isHeldReferral(row),
      onClick: (row) => defs.onRelease(row),
    },
    {
      icon: 'pi pi-ban',
      tooltip: translate.instant('pages.loyalty_referrals.actions.reject'),
      color: 'danger',
      visible: (row) => isHeldReferral(row),
      onClick: (row) => defs.onReject(row),
    },
  ];
}
