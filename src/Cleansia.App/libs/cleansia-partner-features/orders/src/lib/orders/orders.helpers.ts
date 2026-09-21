import { formatDate } from '@cleansia/utils';
import { FilterChip, ICleansiaSelectOption } from '@cleansia/components';
import { OrderFilter } from '@cleansia/models';
import { OrderStatus, PaymentStatus } from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { OrderFilterFormValue } from './orders.models';

// --- Filter options builders ---

export function buildOrderStatusOptions(
  translate: TranslateService
): ICleansiaSelectOption[] {
  return [
    { label: translate.instant('enums.order_status.new'), value: OrderStatus.New },
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
  formValue: OrderFilterFormValue,
  orderStatusMultiOptions: ICleansiaSelectOption[],
  paymentStatusMultiOptions: ICleansiaSelectOption[],
  translate: TranslateService
): FilterChip[] {
  const chips: FilterChip[] = [];

  if (formValue.customerName) {
    chips.push({
      key: 'customerName',
      label: translate.instant('pages.orders.filters.customer_name'),
      value: formValue.customerName,
    });
  }

  if (formValue.customerEmail) {
    chips.push({
      key: 'customerEmail',
      label: translate.instant('pages.orders.filters.customer_email'),
      value: formValue.customerEmail,
    });
  }

  if (formValue.displayOrderNumber) {
    chips.push({
      key: 'displayOrderNumber',
      label: translate.instant('pages.orders.filters.order_number'),
      value: formValue.displayOrderNumber,
    });
  }

  if (formValue.orderStatuses?.length) {
    const statusNames = formValue.orderStatuses
      .map((id) => orderStatusMultiOptions.find((o) => o.value === id)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'orderStatuses',
      label: translate.instant('pages.orders.filters.order_status'),
      value: statusNames,
      controls: ['orderStatuses', ...orderStatusMultiOptions.map((o) => `orderStatus_${o.value}`)],
    });
  }

  if (formValue.paymentStatuses?.length) {
    const statusNames = formValue.paymentStatuses
      .map((id) => paymentStatusMultiOptions.find((o) => o.value === id)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'paymentStatuses',
      label: translate.instant('pages.orders.filters.payment_status'),
      value: statusNames,
      controls: ['paymentStatuses', ...paymentStatusMultiOptions.map((o) => `paymentStatus_${o.value}`)],
    });
  }

  if (formValue.cleaningDateFrom) {
    chips.push({
      key: 'cleaningDateFrom',
      label: translate.instant('pages.orders.filters.cleaning_date_from'),
      value: formatDate(formValue.cleaningDateFrom, translate.currentLang),
    });
  }

  if (formValue.cleaningDateTo) {
    chips.push({
      key: 'cleaningDateTo',
      label: translate.instant('pages.orders.filters.cleaning_date_to'),
      value: formatDate(formValue.cleaningDateTo, translate.currentLang),
    });
  }

  return chips;
}

// --- Build OrderFilter from form values ---

export function buildOrderFilter(formValues: OrderFilterFormValue): OrderFilter {
  return new OrderFilter({
    customerName: formValues.customerName || undefined,
    customerEmail: formValues.customerEmail || undefined,
    displayOrderNumber: formValues.displayOrderNumber || undefined,
    orderStatuses: formValues.orderStatuses?.length ? formValues.orderStatuses : undefined,
    paymentStatuses: formValues.paymentStatuses?.length ? formValues.paymentStatuses : undefined,
    cleaningDateFrom: formValues.cleaningDateFrom || undefined,
    cleaningDateTo: formValues.cleaningDateTo || undefined,
  });
}
