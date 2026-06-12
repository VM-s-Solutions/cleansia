import { AdminReferralListItem, ReferralStatus } from '@cleansia/admin-services';
import { TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export const REFERRAL_STATUS_LABEL_KEYS: Readonly<
  Record<ReferralStatus, string>
> = {
  [ReferralStatus.Accepted]: 'pages.loyalty_referrals.status.Accepted',
  [ReferralStatus.Qualified]: 'pages.loyalty_referrals.status.Qualified',
  [ReferralStatus.Expired]: 'pages.loyalty_referrals.status.Expired',
  [ReferralStatus.Reversed]: 'pages.loyalty_referrals.status.Reversed',
};

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
export const REFERRAL_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'referral.not_qualified': 'errors.referral.not_qualified',
  'referral.not_accepted': 'errors.referral.not_accepted',
  'referral.reason_required': 'errors.referral.reason_required',
  'referral.not_found': 'errors.referral.not_found',
};

export const REFERRAL_FALLBACK_ERROR_KEY = 'errors.referral.action_failed';

export function resolveReferralErrorKey(error: unknown): string {
  const apiError = error as {
    result?: { detail?: string; title?: string };
    response?: string;
  };
  let code = apiError?.result?.detail || apiError?.result?.title;

  if (!code && apiError?.response) {
    try {
      const parsed = JSON.parse(apiError.response) as {
        detail?: string;
        title?: string;
      };
      code = parsed.detail || parsed.title;
    } catch {
      code = undefined;
    }
  }

  if (code && REFERRAL_ERROR_KEY_MAP[code]) {
    return REFERRAL_ERROR_KEY_MAP[code];
  }
  return REFERRAL_FALLBACK_ERROR_KEY;
}
