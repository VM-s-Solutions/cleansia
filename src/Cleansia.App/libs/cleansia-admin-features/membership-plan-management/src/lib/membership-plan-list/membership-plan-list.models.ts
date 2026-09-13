import { TemplateRef } from '@angular/core';
import { BillingInterval, MembershipPlanListItem } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export const BILLING_INTERVAL_WIRE = {
  monthly: BillingInterval.Monthly,
  yearly: BillingInterval.Yearly,
} as const;

export type BillingIntervalWireValue =
  (typeof BILLING_INTERVAL_WIRE)[keyof typeof BILLING_INTERVAL_WIRE];

export const BILLING_INTERVAL_LABEL_KEYS: Readonly<Record<number, string>> = {
  [BILLING_INTERVAL_WIRE.monthly]: 'pages.membership_plans.interval.Monthly',
  [BILLING_INTERVAL_WIRE.yearly]: 'pages.membership_plans.interval.Yearly',
};

export function toBillingIntervalWireValue(
  value: BillingInterval | number | undefined
): BillingIntervalWireValue {
  return Number(value) === BILLING_INTERVAL_WIRE.yearly
    ? BILLING_INTERVAL_WIRE.yearly
    : BILLING_INTERVAL_WIRE.monthly;
}

export const UNPRICED_PLAN_CELL = '—';

/**
 * A plan with no row in the platform default currency is not on sale there — that is a real
 * state, so the cell reads a dash and never "0", which would read as free.
 */
export function formatPlanPrice(
  value: number | undefined | null,
  currencyCode: string | undefined | null
): string {
  if (value == null) return UNPRICED_PLAN_CELL;
  return `${value.toFixed(2)} ${currencyCode ?? ''}`.trimEnd();
}

export function getMembershipPlanTableDefinition(
  defs: {
    onEdit: (row: MembershipPlanListItem) => void;
    onDeactivate: (row: MembershipPlanListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<MembershipPlanListItem>
): {
  columns: TableColumn<MembershipPlanListItem>[];
  actions: TableAction<MembershipPlanListItem>[];
} {
  return {
    columns: [
      {
        id: 'code',
        field: 'code',
        header: translate.instant('pages.membership_plans.columns.code'),
        width: '11%',
      },
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.membership_plans.columns.name'),
        width: '13%',
      },
      {
        id: 'billingInterval',
        field: 'billingInterval',
        header: translate.instant('pages.membership_plans.columns.interval'),
        getValue: (row) => {
          const labelKey = BILLING_INTERVAL_LABEL_KEYS[Number(row.billingInterval)];
          return labelKey ? translate.instant(labelKey) : '';
        },
        width: '8%',
      },
      {
        id: 'price',
        field: 'price',
        header: translate.instant('pages.membership_plans.columns.price'),
        getValue: (row) => formatPlanPrice(row.price, row.currencyCode),
        width: '10%',
      },
      {
        id: 'monthlyEquivalentPrice',
        field: 'monthlyEquivalentPrice',
        header: translate.instant(
          'pages.membership_plans.columns.monthly_equivalent'
        ),
        getValue: (row) =>
          formatPlanPrice(row.monthlyEquivalentPrice, row.currencyCode),
        width: '11%',
      },
      {
        id: 'currencyCode',
        field: 'currencyCode',
        header: translate.instant('pages.membership_plans.columns.currency'),
        getValue: (row) => row.currencyCode ?? '',
        width: '7%',
      },
      {
        id: 'discountPercentage',
        field: 'discountPercentage',
        header: translate.instant('pages.membership_plans.columns.discount'),
        getValue: (row) =>
          row.discountPercentage != null ? `${row.discountPercentage}%` : '—',
        width: '7%',
      },
      {
        id: 'trialPeriodDays',
        field: 'trialPeriodDays',
        header: translate.instant('pages.membership_plans.columns.trial_days'),
        getValue: (row) => `${row.trialPeriodDays ?? 0}`,
        width: '7%',
      },
      {
        id: 'freeCancellationWindowHours',
        field: 'freeCancellationWindowHours',
        header: translate.instant(
          'pages.membership_plans.columns.free_cancel_window'
        ),
        getValue: (row) => `${row.freeCancellationWindowHours ?? 0}`,
        width: '8%',
      },
      {
        id: 'allowsExpressUpgrade',
        field: 'allowsExpressUpgrade',
        header: translate.instant('pages.membership_plans.columns.express'),
        getValue: (row) =>
          translate.instant(row.allowsExpressUpgrade ? 'global.yes' : 'global.no'),
        width: '7%',
      },
      {
        id: 'isActive',
        field: 'isActive',
        header: translate.instant('pages.membership_plans.columns.status'),
        customTemplate: statusTemplate,
        width: '11%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('global.actions.edit'),
        color: 'warning',
        onClick: (row) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-ban',
        tooltip: translate.instant('pages.membership_plans.actions.deactivate'),
        color: 'danger',
        visible: (row) => row.isActive === true,
        onClick: (row) => defs.onDeactivate(row),
      },
    ],
  };
}

/**
 * Backend BusinessErrorMessage code -> i18n key, explicit so plan-management
 * never depends on the snackbar's best-effort normalization (mirrors the
 * disputes-management map).
 */
export const MEMBERSHIP_PLAN_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'membership.plan.code_already_exists':
    'api.membership.plan.code_already_exists',
  'membership.plan.discount_out_of_range':
    'api.membership.plan.discount_out_of_range',
  'membership.plan.not_found': 'api.membership.plan.not_found',
  'membership.plan.stripe_price_already_used':
    'api.membership.plan.stripe_price_already_used',
  'currency.not_found': 'api.currency.not_found',
};

export const MEMBERSHIP_PLAN_FALLBACK_ERROR_KEY =
  'api.membership.plan.action_failed';

export function resolveMembershipPlanErrorKey(error: unknown): string {
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

  if (code && MEMBERSHIP_PLAN_ERROR_KEY_MAP[code]) {
    return MEMBERSHIP_PLAN_ERROR_KEY_MAP[code];
  }
  return MEMBERSHIP_PLAN_FALLBACK_ERROR_KEY;
}
