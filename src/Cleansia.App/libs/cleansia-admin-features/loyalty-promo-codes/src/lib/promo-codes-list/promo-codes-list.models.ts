import { PromoCodeListItem, PromoCodeType } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export type PromoCodeStatusBadge = 'active' | 'inactive' | 'expired';

export function getPromoCodeStatus(row: PromoCodeListItem): PromoCodeStatusBadge {
  if (!row.isActive) return 'inactive';
  if (row.validUntil && row.validUntil.getTime() < Date.now()) return 'expired';
  return 'active';
}

export function formatDiscount(
  row: PromoCodeListItem,
  translate: TranslateService
): string {
  if (row.type === PromoCodeType.PercentDiscount) {
    const pct = row.discountPercent ?? 0;
    // Backend stores percent as 0..1; UI shows 0..100.
    return `${Math.round(pct * 100)}%`;
  }
  const amount = row.discountAmount ?? 0;
  const code = row.currencyCode ?? '';
  return `${amount} ${code}`.trim();
}

export function formatValidity(
  row: PromoCodeListItem,
  translate: TranslateService,
  formatDate: (d?: Date) => string
): string {
  if (!row.validFrom && !row.validUntil) {
    return translate.instant('pages.promo_codes.validity.indefinite');
  }
  if (row.validFrom && row.validUntil) {
    return translate.instant('pages.promo_codes.validity.range', {
      from: formatDate(row.validFrom),
      until: formatDate(row.validUntil),
    });
  }
  if (row.validFrom) {
    return translate.instant('pages.promo_codes.validity.from', {
      date: formatDate(row.validFrom),
    });
  }
  return translate.instant('pages.promo_codes.validity.until', {
    date: formatDate(row.validUntil!),
  });
}

export function formatGlobalLimit(
  row: PromoCodeListItem,
  translate: TranslateService
): string {
  if (row.globalMaxRedemptions == null) {
    return translate.instant('pages.promo_codes.unlimited');
  }
  const used = row.currentRedemptionsCount ?? 0;
  return `${used} / ${row.globalMaxRedemptions}`;
}

export function formatType(
  row: PromoCodeListItem,
  translate: TranslateService
): string {
  if (row.type === PromoCodeType.PercentDiscount) {
    return translate.instant('pages.promo_codes.type.percent');
  }
  if (row.type === PromoCodeType.FixedDiscount) {
    return translate.instant('pages.promo_codes.type.fixed');
  }
  return '';
}

export function formatStatus(
  row: PromoCodeListItem,
  translate: TranslateService
): string {
  const status = getPromoCodeStatus(row);
  return translate.instant(`pages.promo_codes.status_filter_${status}`);
}

export function getPromoCodeTableDefinition(
  defs: {
    onView: (row: PromoCodeListItem) => void;
    onEdit: (row: PromoCodeListItem) => void;
    onDeactivate: (row: PromoCodeListItem) => void;
  },
  translate: TranslateService,
  formatDate: (d?: Date) => string
): {
  columns: TableColumn<PromoCodeListItem>[];
  actions: TableAction<PromoCodeListItem>[];
} {
  return {
    columns: [
      {
        id: 'code',
        field: 'code',
        header: translate.instant('pages.promo_codes.column.code'),
        width: '14%',
      },
      {
        id: 'type',
        field: 'type',
        header: translate.instant('pages.promo_codes.column.type'),
        getValue: (row) => formatType(row, translate),
        width: '10%',
      },
      {
        id: 'discount',
        field: 'discountPercent',
        header: translate.instant('pages.promo_codes.column.discount'),
        getValue: (row) => formatDiscount(row, translate),
        width: '10%',
      },
      {
        id: 'minOrder',
        field: 'minimumOrderAmount',
        header: translate.instant('pages.promo_codes.column.min_order'),
        getValue: (row) =>
          row.minimumOrderAmount != null ? `${row.minimumOrderAmount}` : '—',
        width: '10%',
      },
      {
        id: 'perUser',
        field: 'maxRedemptionsPerUser',
        header: translate.instant('pages.promo_codes.column.per_user'),
        getValue: (row) => `${row.maxRedemptionsPerUser}`,
        width: '8%',
      },
      {
        id: 'global',
        field: 'globalMaxRedemptions',
        header: translate.instant('pages.promo_codes.column.global'),
        getValue: (row) => formatGlobalLimit(row, translate),
        width: '12%',
      },
      {
        id: 'validity',
        field: 'validUntil',
        header: translate.instant('pages.promo_codes.column.validity'),
        getValue: (row) => formatValidity(row, translate, formatDate),
        width: '18%',
      },
      {
        id: 'status',
        field: 'isActive',
        header: translate.instant('pages.promo_codes.column.status'),
        getValue: (row) => formatStatus(row, translate),
        width: '10%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('global.actions.view'),
        color: 'info',
        onClick: (row) => defs.onView(row),
      },
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('global.actions.edit'),
        color: 'warning',
        onClick: (row) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-ban',
        tooltip: translate.instant(
          'pages.promo_codes.detail.deactivate_button'
        ),
        color: 'danger',
        visible: (row) => getPromoCodeStatus(row) === 'active',
        onClick: (row) => defs.onDeactivate(row),
      },
    ],
  };
}
