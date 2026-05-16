import { toKebabCase, toSnakeCase } from '@cleansia/utils';
import { ICleansiaSelectOption } from '@cleansia/components';
import { OrderFilter } from '@cleansia/models';
import {
  OrderListItem,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { FilterChip } from './orders.models';

// --- Status CSS class helpers ---

export function getStatusClass(order: OrderListItem): string {
  const statusName = toKebabCase(order.paymentStatus?.name) || 'pending';
  return `status-badge status-${statusName}`;
}

export function getOrderStatusClass(order: OrderListItem): string {
  const statusName = toKebabCase(order.orderStatus?.name) || 'pending';
  return `order-status-badge status-${statusName}`;
}

// --- Translation helpers ---

export function getTranslatedPaymentStatus(
  paymentStatus: { name?: string } | null | undefined,
  translate: TranslateService
): string {
  if (!paymentStatus?.name) return '';
  const key = `enums.payment_status.${toSnakeCase(paymentStatus.name)}`;
  return translate.instant(key);
}

export function getTranslatedOrderStatus(
  orderStatus: { name?: string } | null | undefined,
  translate: TranslateService
): string {
  if (!orderStatus?.name) return '';
  const key = `enums.order_status.${toSnakeCase(orderStatus.name)}`;
  return translate.instant(key);
}

// --- Filter options builders ---

export function buildOrderStatusOptions(
  translate: TranslateService
): ICleansiaSelectOption[] {
  return [
    { label: translate.instant('enums.order_status.pending'), value: OrderStatus.Pending },
    { label: translate.instant('enums.order_status.confirmed'), value: OrderStatus.Confirmed },
    { label: translate.instant('enums.order_status.on_the_way'), value: OrderStatus.OnTheWay },
    { label: translate.instant('enums.order_status.in_progress'), value: OrderStatus.InProgress },
    { label: translate.instant('enums.order_status.completed'), value: OrderStatus.Completed },
    { label: translate.instant('enums.order_status.cancelled'), value: OrderStatus.Cancelled },
  ];
}

export function buildPaymentStatusOptions(
  translate: TranslateService
): ICleansiaSelectOption[] {
  return [
    { label: translate.instant('enums.payment_status.pending'), value: PaymentStatus.Pending },
    { label: translate.instant('enums.payment_status.paid'), value: PaymentStatus.Paid },
    { label: translate.instant('enums.payment_status.failed'), value: PaymentStatus.Failed },
    { label: translate.instant('enums.payment_status.refunded'), value: PaymentStatus.Refunded },
  ];
}

// --- Filter chips ---

export function buildActiveFilterChips(
  formValue: Record<string, any>,
  orderStatusMultiOptions: ICleansiaSelectOption[],
  paymentStatusMultiOptions: ICleansiaSelectOption[],
  translate: TranslateService
): FilterChip[] {
  const chips: FilterChip[] = [];

  if (formValue['customerName']) {
    chips.push({
      key: 'customerName',
      label: translate.instant('pages.orders.filters.customer_name'),
      value: formValue['customerName'],
    });
  }

  if (formValue['customerEmail']) {
    chips.push({
      key: 'customerEmail',
      label: translate.instant('pages.orders.filters.customer_email'),
      value: formValue['customerEmail'],
    });
  }

  if (formValue['displayOrderNumber']) {
    chips.push({
      key: 'displayOrderNumber',
      label: translate.instant('pages.orders.filters.order_number'),
      value: formValue['displayOrderNumber'],
    });
  }

  if (formValue['orderStatuses']?.length) {
    const statusNames = formValue['orderStatuses']
      .map((id: number) => orderStatusMultiOptions.find((o) => o.value === id)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'orderStatuses',
      label: translate.instant('pages.orders.filters.order_status'),
      value: statusNames,
    });
  }

  if (formValue['paymentStatuses']?.length) {
    const statusNames = formValue['paymentStatuses']
      .map((id: number) => paymentStatusMultiOptions.find((o) => o.value === id)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'paymentStatuses',
      label: translate.instant('pages.orders.filters.payment_status'),
      value: statusNames,
    });
  }

  if (formValue['cleaningDateFrom']) {
    chips.push({
      key: 'cleaningDateFrom',
      label: translate.instant('pages.orders.filters.cleaning_date_from'),
      value: new Date(formValue['cleaningDateFrom']).toLocaleDateString(),
    });
  }

  if (formValue['cleaningDateTo']) {
    chips.push({
      key: 'cleaningDateTo',
      label: translate.instant('pages.orders.filters.cleaning_date_to'),
      value: new Date(formValue['cleaningDateTo']).toLocaleDateString(),
    });
  }

  return chips;
}

// --- Build OrderFilter from form values ---

export function buildOrderFilter(formValues: Record<string, any>): OrderFilter {
  return new OrderFilter({
    customerName: formValues['customerName'] || undefined,
    customerEmail: formValues['customerEmail'] || undefined,
    displayOrderNumber: formValues['displayOrderNumber'] || undefined,
    orderStatuses:
      formValues['orderStatuses'] && formValues['orderStatuses'].length > 0
        ? formValues['orderStatuses']
        : undefined,
    paymentStatuses:
      formValues['paymentStatuses'] && formValues['paymentStatuses'].length > 0
        ? formValues['paymentStatuses']
        : undefined,
    cleaningDateFrom: formValues['cleaningDateFrom'] || undefined,
    cleaningDateTo: formValues['cleaningDateTo'] || undefined,
  });
}
