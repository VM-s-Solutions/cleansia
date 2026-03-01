import { TemplateRef } from '@angular/core';
import {
  OrderListItem,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getOrderTableDefinition(
  defs: {
    onViewDetails: (row: OrderListItem) => void;
  },
  translate: TranslateService,
  orderStatusTemplate?: TemplateRef<OrderListItem>,
  paymentStatusTemplate?: TemplateRef<OrderListItem>
): { columns: TableColumn<OrderListItem>[]; actions: TableAction<OrderListItem>[] } {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        field: 'displayOrderNumber',
        header: translate.instant('pages.order_management.order_number'),
        sortable: true,
        width: '12%',
      },
      {
        id: 'customerName',
        field: 'customerName',
        header: translate.instant('pages.order_management.customer_name'),
        sortable: true,
        width: '15%',
      },
      {
        id: 'customerEmail',
        field: 'customerEmail',
        header: translate.instant('pages.order_management.customer_email'),
        width: '15%',
      },
      {
        id: 'cleaningDateTime',
        field: 'cleaningDateTime',
        header: translate.instant('pages.order_management.cleaning_date'),
        sortable: true,
        width: '12%',
        getValue: (row: OrderListItem) => {
          if (!row?.cleaningDateTime) return '';
          const date =
            row.cleaningDateTime instanceof Date
              ? row.cleaningDateTime
              : new Date(row.cleaningDateTime);
          return (
            date.toLocaleDateString('en-GB') +
            ' ' +
            date.toLocaleTimeString('en-GB', {
              hour: '2-digit',
              minute: '2-digit',
            })
          );
        },
      },
      {
        id: 'totalPrice',
        field: 'totalPrice',
        header: translate.instant('pages.order_management.total_price'),
        sortable: true,
        width: '10%',
        getValue: (row: OrderListItem) => {
          if (!row) return '';
          const symbol = row.currency?.symbol || 'EUR';
          return `${row.totalPrice?.toFixed(2)} ${symbol}`;
        },
      },
      {
        id: 'orderStatus',
        field: 'orderStatus',
        header: translate.instant(
          'pages.order_management.order_status_label'
        ),
        sortable: true,
        width: '10%',
        customTemplate: orderStatusTemplate,
      },
      {
        id: 'paymentStatus',
        field: 'paymentStatus',
        header: translate.instant(
          'pages.order_management.payment_status_label'
        ),
        width: '10%',
        customTemplate: paymentStatusTemplate,
      },
      {
        id: 'assignedEmployees',
        field: 'assignedEmployees',
        header: translate.instant(
          'pages.order_management.assigned_employees'
        ),
        width: '8%',
        getValue: (row: OrderListItem) => {
          if (!row) return '';
          return `${row.assignedEmployeesCount || 0}/${
            row.requiredEmployees || 0
          }`;
        },
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.order_management.view_details'),
        color: 'info',
        onClick: (row: OrderListItem) => defs.onViewDetails(row),
      },
    ],
  };
}

export function getOrderStatusClass(order: OrderListItem): string {
  if (!order.orderStatus) return 'order-status-badge status-pending';
  switch (order.orderStatus.value) {
    case OrderStatus.Pending:
      return 'order-status-badge status-pending';
    case OrderStatus.Confirmed:
      return 'order-status-badge status-confirmed';
    case OrderStatus.InProgress:
      return 'order-status-badge status-inprogress';
    case OrderStatus.Completed:
      return 'order-status-badge status-completed';
    case OrderStatus.Cancelled:
      return 'order-status-badge status-cancelled';
    default:
      return 'order-status-badge status-pending';
  }
}

export function getPaymentStatusClass(order: OrderListItem): string {
  if (!order.paymentStatus) return 'payment-status-badge status-pending';
  switch (order.paymentStatus.value) {
    case PaymentStatus.Pending:
      return 'payment-status-badge status-pending';
    case PaymentStatus.Paid:
      return 'payment-status-badge status-paid';
    case PaymentStatus.Failed:
      return 'payment-status-badge status-failed';
    case PaymentStatus.Refunded:
      return 'payment-status-badge status-refunded';
    case PaymentStatus.Disputed:
      return 'payment-status-badge status-disputed';
    default:
      return 'payment-status-badge status-pending';
  }
}
