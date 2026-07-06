import { TemplateRef } from '@angular/core';
import { BillingInterval, MembershipPlanListItem } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

/**
 * BillingInterval wire values — the backend serializes enums as ints
 * (Monthly=1, Yearly=2); the generated string enum lies about the runtime
 * shape until the admin client is regenerated.
 */
export const BILLING_INTERVAL_WIRE = {
  monthly: 1,
  yearly: 2,
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
  if (value === BillingInterval.Yearly) return BILLING_INTERVAL_WIRE.yearly;
  return Number(value) === BILLING_INTERVAL_WIRE.yearly
    ? BILLING_INTERVAL_WIRE.yearly
    : BILLING_INTERVAL_WIRE.monthly;
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
        width: '12%',
      },
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.membership_plans.columns.name'),
        width: '14%',
      },
      {
        id: 'billingInterval',
        field: 'billingInterval',
        header: translate.instant('pages.membership_plans.columns.interval'),
        getValue: (row) => {
          const labelKey = BILLING_INTERVAL_LABEL_KEYS[Number(row.billingInterval)];
          return labelKey ? translate.instant(labelKey) : '';
        },
        width: '9%',
      },
      {
        id: 'monthlyPriceCzk',
        field: 'monthlyPriceCzk',
        header: translate.instant('pages.membership_plans.columns.price'),
        getValue: (row) => formatCzk(row.monthlyPriceCzk),
        width: '10%',
      },
      {
        id: 'monthlyEquivalentPriceCzk',
        field: 'monthlyEquivalentPriceCzk',
        header: translate.instant(
          'pages.membership_plans.columns.monthly_equivalent'
        ),
        getValue: (row) => formatCzk(row.monthlyEquivalentPriceCzk),
        width: '11%',
      },
      {
        id: 'discountPercentage',
        field: 'discountPercentage',
        header: translate.instant('pages.membership_plans.columns.discount'),
        getValue: (row) =>
          row.discountPercentage != null ? `${row.discountPercentage}%` : '—',
        width: '8%',
      },
      {
        id: 'trialPeriodDays',
        field: 'trialPeriodDays',
        header: translate.instant('pages.membership_plans.columns.trial_days'),
        getValue: (row) => `${row.trialPeriodDays ?? 0}`,
        width: '8%',
      },
      {
        id: 'freeCancellationWindowHours',
        field: 'freeCancellationWindowHours',
        header: translate.instant(
          'pages.membership_plans.columns.free_cancel_window'
        ),
        getValue: (row) => `${row.freeCancellationWindowHours ?? 0}`,
        width: '9%',
      },
      {
        id: 'allowsExpressUpgrade',
        field: 'allowsExpressUpgrade',
        header: translate.instant('pages.membership_plans.columns.express'),
        getValue: (row) =>
          translate.instant(row.allowsExpressUpgrade ? 'global.yes' : 'global.no'),
        width: '8%',
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

function formatCzk(value: number | undefined | null): string {
  if (value == null) return '—';
  return `${value.toFixed(2)} CZK`;
}

/**
 * Backend BusinessErrorMessage code -> i18n key, explicit so plan-management
 * never depends on the snackbar's best-effort normalization (mirrors the
 * disputes-management map).
 */
export const MEMBERSHIP_PLAN_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'membership.plan.code_already_exists':
    'errors.membership.plan.code_already_exists',
  'membership.plan.discount_out_of_range':
    'errors.membership.plan.discount_out_of_range',
  'membership.plan.not_found': 'errors.membership.plan.not_found',
};

export const MEMBERSHIP_PLAN_FALLBACK_ERROR_KEY =
  'errors.membership.plan.action_failed';

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
