import { TemplateRef } from '@angular/core';
import { HelpStep, StatusFlowItem, TableAction, TableColumn } from '@cleansia/components';
import { OrderListItem, OrderStatus } from '@cleansia/partner-services';

export interface FilterChip {
  key: string;
  label: string;
  value: string;
}

export const ORDERS_HELP_STEPS: HelpStep[] = [
  {
    icon: 'pi pi-search',
    titleKey: 'help.orders.step1_title',
    descriptionKey: 'help.orders.step1_desc',
  },
  {
    icon: 'pi pi-check-circle',
    titleKey: 'help.orders.step2_title',
    descriptionKey: 'help.orders.step2_desc',
  },
  {
    icon: 'pi pi-briefcase',
    titleKey: 'help.orders.step3_title',
    descriptionKey: 'help.orders.step3_desc',
  },
  {
    icon: 'pi pi-wallet',
    titleKey: 'help.orders.step4_title',
    descriptionKey: 'help.orders.step4_desc',
  },
];

export const ORDER_STATUS_FLOW: StatusFlowItem[] = [
  {
    statusKey: 'enums.order_status.pending',
    descriptionKey: 'help.orders.status.pending_desc',
    colorClass: 'status-pending',
  },
  {
    statusKey: 'enums.order_status.confirmed',
    descriptionKey: 'help.orders.status.confirmed_desc',
    colorClass: 'status-confirmed',
  },
  {
    statusKey: 'enums.order_status.in_progress',
    descriptionKey: 'help.orders.status.in_progress_desc',
    colorClass: 'status-in-progress',
  },
  {
    statusKey: 'enums.order_status.completed',
    descriptionKey: 'help.orders.status.completed_desc',
    colorClass: 'status-completed',
  },
  {
    statusKey: 'enums.order_status.cancelled',
    descriptionKey: 'help.orders.status.cancelled_desc',
    colorClass: 'status-cancelled',
  },
];

export const PAYMENT_STATUS_FLOW: StatusFlowItem[] = [
  {
    statusKey: 'enums.payment_status.pending',
    descriptionKey: 'help.orders.payment.pending_desc',
    colorClass: 'status-pending',
  },
  {
    statusKey: 'enums.payment_status.paid',
    descriptionKey: 'help.orders.payment.paid_desc',
    colorClass: 'status-paid',
  },
  {
    statusKey: 'enums.payment_status.failed',
    descriptionKey: 'help.orders.payment.failed_desc',
    colorClass: 'status-failed',
  },
  {
    statusKey: 'enums.payment_status.refunded',
    descriptionKey: 'help.orders.payment.refunded_desc',
    colorClass: 'status-refunded',
  },
];

export function getAvailableOrdersTableDefinition(
  defs: {
    onTakeOrder: (row: OrderListItem) => void;
  },
  statusTemplate?: TemplateRef<OrderListItem>,
  orderStatusTemplate?: TemplateRef<OrderListItem>
): {
  columns: TableColumn<OrderListItem>[];
  actions: TableAction<OrderListItem>[];
} {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        field: 'displayOrderNumber',
        header: 'pages.orders.order_number',
        sortable: true,
        width: '12%',
      },
      {
        id: 'cleaningDateTime',
        field: 'cleaningDateTime',
        header: 'pages.orders.cleaning_date',
        getValue: (row?: OrderListItem) =>
          row?.cleaningDateTime
            ? new Date(row.cleaningDateTime).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
            : '',
        sortable: true,
        width: '12%',
      },
      {
        id: 'address',
        field: 'customerAddress',
        header: 'pages.orders.address',
        getValue: (row?: OrderListItem) =>
          `${row?.customerAddress || ''}`.trim().replace(/^,\s*/, ''),
        width: '20%',
      },
      {
        id: 'totalPrice',
        field: 'totalPrice',
        header: 'pages.orders.total_price',
        getValue: (row?: OrderListItem) =>
          row?.totalPrice
            ? Number(row.totalPrice).toLocaleString('en-GB', {
                style: 'currency',
                currency: row?.currency?.code || 'CZK',
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
              })
            : '',
        sortable: true,
        width: '12%',
        align: 'right',
      },
      {
        id: 'availableSpots',
        field: 'availableSpots',
        header: 'pages.orders.available_spots',
        getValue: (row?: OrderListItem) =>
          `${row?.availableSpots || 0} / ${row?.maxEmployees || 0}`,
        width: '10%',
      },
      {
        id: 'paymentStatus',
        field: 'paymentStatus',
        header: 'pages.orders.payment_status',
        customTemplate: statusTemplate,
        width: '12%',
      },
      {
        id: 'orderStatus',
        field: 'orderStatus',
        header: 'pages.orders.order_status',
        customTemplate: orderStatusTemplate,
        width: '12%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-check',
        tooltip: 'pages.orders.take_order',
        color: 'success',
        onClick: (row: OrderListItem) => defs.onTakeOrder(row),
        visible: (row: OrderListItem) => {
          const status = row.orderStatus?.value;
          const isTakeable =
            status === OrderStatus.New ||
            status === OrderStatus.Pending ||
            status === OrderStatus.Confirmed;
          return isTakeable && (row.availableSpots ?? 0) > 0;
        },
      },
    ],
  };
}

export function getMyOrdersTableDefinition(
  defs: {
    onStartOrder: (row: OrderListItem) => void;
    onCompleteOrder: (row: OrderListItem) => void;
  },
  statusTemplate?: TemplateRef<OrderListItem>,
  orderStatusTemplate?: TemplateRef<OrderListItem>
): {
  columns: TableColumn<OrderListItem>[];
  actions: TableAction<OrderListItem>[];
} {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        field: 'displayOrderNumber',
        header: 'pages.orders.order_number',
        sortable: true,
        width: '12%',
      },
      {
        id: 'customerName',
        field: 'customerName',
        header: 'pages.orders.customer_name',
        sortable: true,
        width: '15%',
      },
      {
        id: 'customerPhone',
        field: 'customerPhone',
        header: 'pages.orders.customer_phone',
        width: '12%',
      },
      {
        id: 'cleaningDateTime',
        field: 'cleaningDateTime',
        header: 'pages.orders.cleaning_date',
        getValue: (row?: OrderListItem) =>
          row?.cleaningDateTime
            ? new Date(row.cleaningDateTime).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
            : '',
        sortable: true,
        width: '12%',
      },
      {
        id: 'address',
        field: 'customerAddress',
        header: 'pages.orders.address',
        getValue: (row?: OrderListItem) =>
          `${row?.customerAddress || ''}`.trim().replace(/^,\s*/, ''),
        width: '18%',
      },
      {
        id: 'totalPrice',
        field: 'totalPrice',
        header: 'pages.orders.total_price',
        getValue: (row?: OrderListItem) =>
          row?.totalPrice
            ? Number(row.totalPrice).toLocaleString('en-GB', {
                style: 'currency',
                currency: row?.currency?.code || 'CZK',
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
              })
            : '',
        sortable: true,
        width: '12%',
        align: 'right',
      },
      {
        id: 'orderStatus',
        field: 'orderStatus',
        header: 'pages.orders.order_status',
        customTemplate: orderStatusTemplate,
        width: '12%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-play',
        tooltip: 'pages.orders.start_order',
        color: 'primary',
        onClick: (row: OrderListItem) => defs.onStartOrder(row),
        visible: (row: OrderListItem) => {
          const v = row.orderStatus?.value;
          return v === OrderStatus.Confirmed || v === OrderStatus.OnTheWay;
        },
      },
      {
        icon: 'pi pi-check-circle',
        tooltip: 'pages.orders.complete_order.title',
        color: 'success',
        onClick: (row: OrderListItem) => defs.onCompleteOrder(row),
        visible: (row: OrderListItem) =>
          row.orderStatus.value === OrderStatus.InProgress,
      },
    ],
  };
}
