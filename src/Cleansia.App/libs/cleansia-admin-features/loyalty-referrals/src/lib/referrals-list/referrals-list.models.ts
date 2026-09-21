import { AdminReferralListItem, ReferralStatus } from '@cleansia/admin-services';
import { TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

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

