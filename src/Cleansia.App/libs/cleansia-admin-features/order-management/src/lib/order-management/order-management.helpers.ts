import {
  OrderListItem,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';

// --- Status label helpers ---

const ORDER_STATUS_TRANSLATION_MAP: Record<OrderStatus, string> = {
  [OrderStatus.New]: 'pages.order_management.order_status.new',
  [OrderStatus.Pending]: 'pages.order_management.order_status.pending',
  [OrderStatus.Confirmed]: 'pages.order_management.order_status.confirmed',
  [OrderStatus.OnTheWay]: 'pages.order_management.order_status.on_the_way',
  [OrderStatus.InProgress]: 'pages.order_management.order_status.in_progress',
  [OrderStatus.Completed]: 'pages.order_management.order_status.completed',
  [OrderStatus.Cancelled]: 'pages.order_management.order_status.cancelled',
};

const PAYMENT_STATUS_TRANSLATION_MAP: Record<PaymentStatus, string> = {
  [PaymentStatus.Pending]: 'pages.order_management.payment_status.pending',
  [PaymentStatus.Paid]: 'pages.order_management.payment_status.paid',
  [PaymentStatus.Failed]: 'pages.order_management.payment_status.failed',
  [PaymentStatus.Refunded]: 'pages.order_management.payment_status.refunded',
  [PaymentStatus.Disputed]: 'pages.order_management.payment_status.disputed',
  [PaymentStatus.PartiallyRefunded]:
    'pages.order_management.payment_status.partially_refunded',
};

export function getOrderStatusLabel(
  order: OrderListItem,
  translate: TranslateService
): string {
  if (!order.orderStatus?.value) return '';
  const key =
    ORDER_STATUS_TRANSLATION_MAP[order.orderStatus.value as OrderStatus];
  return key ? translate.instant(key) : order.orderStatus?.name || '';
}

export function getPaymentStatusLabel(
  order: OrderListItem,
  translate: TranslateService
): string {
  if (!order.paymentStatus?.value) return '';
  const key =
    PAYMENT_STATUS_TRANSLATION_MAP[order.paymentStatus.value as PaymentStatus];
  return key ? translate.instant(key) : order.paymentStatus?.name || '';
}

// --- Filter option builders ---

export function buildOrderStatusOptions(
  translate: TranslateService
): { label: string; value: OrderStatus }[] {
  return Object.entries(ORDER_STATUS_TRANSLATION_MAP).map(([value, key]) => ({
    label: translate.instant(key),
    value: value as unknown as OrderStatus,
  }));
}

export function buildPaymentStatusOptions(
  translate: TranslateService
): { label: string; value: PaymentStatus }[] {
  return Object.entries(PAYMENT_STATUS_TRANSLATION_MAP).map(([value, key]) => ({
    label: translate.instant(key),
    value: value as unknown as PaymentStatus,
  }));
}

// --- Filter chip helpers ---

export interface FilterChip {
  key: string;
  label: string;
  value: string;
}

export function buildFilterChips(
  formValues: {
    searchTerm?: string | null;
    orderStatus?: OrderStatus[] | null;
    paymentStatus?: PaymentStatus[] | null;
    cleaningDateFrom?: Date | null;
    cleaningDateTo?: Date | null;
  },
  orderStatusOptions: { label: string; value: OrderStatus }[],
  paymentStatusOptions: { label: string; value: PaymentStatus }[],
  translate: TranslateService
): FilterChip[] {
  const chips: FilterChip[] = [];

  if (formValues.searchTerm) {
    chips.push({
      key: 'searchTerm',
      label: translate.instant('pages.order_management.filters.search'),
      value: formValues.searchTerm,
    });
  }

  if (formValues.orderStatus && formValues.orderStatus.length > 0) {
    const statusLabels = formValues.orderStatus
      .map((s) => orderStatusOptions.find((o) => o.value === s)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'orderStatus',
      label: translate.instant('pages.order_management.filters.order_status'),
      value: statusLabels,
    });
  }

  if (formValues.paymentStatus && formValues.paymentStatus.length > 0) {
    const statusLabels = formValues.paymentStatus
      .map((s) => paymentStatusOptions.find((o) => o.value === s)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'paymentStatus',
      label: translate.instant('pages.order_management.filters.payment_status'),
      value: statusLabels,
    });
  }

  if (formValues.cleaningDateFrom) {
    chips.push({
      key: 'cleaningDateFrom',
      label: translate.instant('pages.order_management.filters.date_from'),
      value: formValues.cleaningDateFrom.toLocaleDateString(),
    });
  }

  if (formValues.cleaningDateTo) {
    chips.push({
      key: 'cleaningDateTo',
      label: translate.instant('pages.order_management.filters.date_to'),
      value: formValues.cleaningDateTo.toLocaleDateString(),
    });
  }

  return chips;
}

// --- Checkbox toggle helper ---

export function toggleStatusInArray<T>(
  currentStatuses: T[],
  status: T,
  checked: boolean
): T[] {
  const result = [...currentStatuses];
  if (checked) {
    if (!result.includes(status)) {
      result.push(status);
    }
  } else {
    const index = result.indexOf(status);
    if (index > -1) {
      result.splice(index, 1);
    }
  }
  return result;
}

// --- Filter payload builder ---

export function buildFilterPayload(formValues: {
  orderStatus?: OrderStatus[] | null;
  paymentStatus?: PaymentStatus[] | null;
  searchTerm?: string | null;
  cleaningDateFrom?: Date | null;
  cleaningDateTo?: Date | null;
}): {
  orderStatuses?: OrderStatus[];
  paymentStatuses?: PaymentStatus[];
  searchTerm?: string;
  cleaningDateFrom?: Date;
  cleaningDateTo?: Date;
} {
  return {
    orderStatuses:
      formValues.orderStatus && formValues.orderStatus.length > 0
        ? formValues.orderStatus
        : undefined,
    paymentStatuses:
      formValues.paymentStatus && formValues.paymentStatus.length > 0
        ? formValues.paymentStatus
        : undefined,
    searchTerm: formValues.searchTerm?.trim() || undefined,
    cleaningDateFrom: formValues.cleaningDateFrom ?? undefined,
    cleaningDateTo: formValues.cleaningDateTo ?? undefined,
  };
}

export const FILTER_FORM_DEFAULTS = {
  orderStatus: [] as OrderStatus[],
  paymentStatus: [] as PaymentStatus[],
  searchTerm: '',
  cleaningDateFrom: null as Date | null,
  cleaningDateTo: null as Date | null,
};

// --- Filter chip removal helper ---

export function getFilterPatchForChipRemoval(key: string): Record<string, any> {
  switch (key) {
    case 'orderStatus':
      return { orderStatus: [] };
    case 'paymentStatus':
      return { paymentStatus: [] };
    case 'cleaningDateFrom':
      return { cleaningDateFrom: null };
    case 'cleaningDateTo':
      return { cleaningDateTo: null };
    default:
      return { [key]: '' };
  }
}
