import { TableAction, TableColumn } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { LoyaltyTier } from '@cleansia/admin-services';

export interface TierRow {
  id: string;
  tier: LoyaltyTier;
  tierName: string;
  threshold: number;
  /** Backend fraction 0..1 — pre-formatted in `discountFormatted` for display. */
  discountPercent: number;
  discountFormatted: string;
  minimumOrderAmountForDiscount?: number;
  minOrderFormatted: string;
  perksJson?: string;
  perksCount: number;
  perksCountFormatted: string;
  // We need the original DTO around for any future actions (audit, etc).
  raw: unknown;
}

/**
 * Column + action factory for the loyalty tier-configs admin grid.
 *
 * Mirrors the contract used by every other admin list view (e.g.
 * `getAdminUserTableDefinition`). Discount/min-order/perks count are
 * pre-formatted in the row mapper so the table can render them as plain
 * strings — no custom templates needed.
 */
export function getTierConfigsTableDefinition(
  defs: {
    onEdit: (row: TierRow) => void;
  },
  translate: TranslateService,
  permissions: PermissionService,
): { columns: TableColumn<TierRow>[]; actions: TableAction<TierRow>[] } {
  return {
    columns: [
      {
        id: 'tier',
        field: 'tierName',
        header: translate.instant('pages.loyalty_tiers.column.tier'),
        width: '20%',
      },
      {
        id: 'threshold',
        numeric: true,
        field: 'threshold',
        header: translate.instant('pages.loyalty_tiers.column.threshold'),
        width: '15%',
      },
      {
        id: 'discount',
        numeric: true,
        field: 'discountFormatted',
        header: translate.instant('pages.loyalty_tiers.column.discount'),
        width: '15%',
      },
      {
        id: 'minOrder',
        numeric: true,
        field: 'minOrderFormatted',
        header: translate.instant('pages.loyalty_tiers.column.min_order'),
        width: '20%',
      },
      {
        id: 'perks',
        numeric: true,
        field: 'perksCountFormatted',
        header: translate.instant('pages.loyalty_tiers.column.perks'),
        width: '15%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('global.actions.edit'),
        color: 'warning',
        visible: () => permissions.hasPolicy(Policy.CanUpdateLoyaltyTierConfig),
        onClick: (row: TierRow) => defs.onEdit(row),
      },
    ],
  };
}
