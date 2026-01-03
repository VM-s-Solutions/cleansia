import { TemplateRef } from '@angular/core';
import {
  OrderListItem,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getOrderTableDefinition(
  defs: {
    onViewDetails: (row: OrderListItem) => void;
  },
  translate: TranslateService,
  orderStatusTemplate?: TemplateRef<OrderListItem>,
  paymentStatusTemplate?: TemplateRef<OrderListItem>
): TableDefinition<OrderListItem> {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        headerName: translate.instant('pages.order_management.order_number'),
        value: 'displayOrderNumber',
        sortable: true,
        sortField: 'displayOrderNumber',
        columnClass: 'width-12',
      },
      {
        id: 'customerName',
        headerName: translate.instant('pages.order_management.customer_name'),
        value: 'customerName',
        sortable: true,
        sortField: 'customerName',
        columnClass: 'width-15',
      },
      {
        id: 'customerEmail',
        headerName: translate.instant('pages.order_management.customer_email'),
        value: 'customerEmail',
        columnClass: 'width-15',
      },
      {
        id: 'cleaningDateTime',
        headerName: translate.instant('pages.order_management.cleaning_date'),
        value: (row?: OrderListItem) => {
          if (!row?.cleaningDateTime) return '';
          const date =
            row.cleaningDateTime instanceof Date
              ? row.cleaningDateTime
              : new Date(row.cleaningDateTime);
          return (
            date.toLocaleDateString('cs-CZ') +
            ' ' +
            date.toLocaleTimeString('cs-CZ', {
              hour: '2-digit',
              minute: '2-digit',
            })
          );
        },
        sortable: true,
        sortField: 'cleaningDateTime',
        columnClass: 'width-12',
      },
      {
        id: 'totalPrice',
        headerName: translate.instant('pages.order_management.total_price'),
        value: (row?: OrderListItem) => {
          if (!row) return '';
          const symbol = row.currency?.symbol || 'CZK';
          return `${row.totalPrice?.toFixed(2)} ${symbol}`;
        },
        sortable: true,
        sortField: 'totalPrice',
        columnClass: 'width-10',
      },
      {
        id: 'orderStatus',
        headerName: translate.instant(
          'pages.order_management.order_status_label'
        ),
        template: orderStatusTemplate,
        sortable: true,
        sortField: 'orderStatus',
        columnClass: 'width-10',
      },
      {
        id: 'paymentStatus',
        headerName: translate.instant(
          'pages.order_management.payment_status_label'
        ),
        template: paymentStatusTemplate,
        columnClass: 'width-10',
      },
      {
        id: 'assignedEmployees',
        headerName: translate.instant(
          'pages.order_management.assigned_employees'
        ),
        value: (row?: OrderListItem) => {
          if (!row) return '';
          return `${row.assignedEmployeesCount || 0}/${
            row.requiredEmployees || 0
          }`;
        },
        columnClass: 'width-8',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.order_management.actions'),
        columnActions: [
          {
            icon: 'pi pi-eye',
            onClick: (row: OrderListItem) => defs.onViewDetails(row),
            buttonPalette: 'p-button-info p-button-sm',
            tooltip: {
              title: translate.instant('pages.order_management.view_details'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-8',
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
