import { TemplateRef } from '@angular/core';
import {
  HelpStep,
  resolveStatusBadge,
  StatusBadgeKind,
  StatusFlowItem,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { OrderListItem, OrderStatus } from '@cleansia/partner-services';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';

export interface OrderFilterFormValue {
  customerName?: string | null;
  customerEmail?: string | null;
  displayOrderNumber?: string | null;
  orderStatuses?: number[] | null;
  paymentStatuses?: number[] | null;
  cleaningDateFrom?: Date | null;
  cleaningDateTo?: Date | null;
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

// The help legend draws the same pill, in the same tone, as the table's badge for that status.
const legendBadgeClass = (kind: StatusBadgeKind, member: string): string =>
  `status-badge status-badge--${resolveStatusBadge(kind, member)?.tone ?? 'neutral'}`;

export const ORDER_STATUS_FLOW: StatusFlowItem[] = [
  {
    statusKey: 'enums.order_status.pending',
    descriptionKey: 'help.orders.status.pending_desc',
    colorClass: legendBadgeClass('order', 'Pending'),
  },
  {
    statusKey: 'enums.order_status.confirmed',
    descriptionKey: 'help.orders.status.confirmed_desc',
    colorClass: legendBadgeClass('order', 'Confirmed'),
  },
  {
    statusKey: 'enums.order_status.in_progress',
    descriptionKey: 'help.orders.status.in_progress_desc',
    colorClass: legendBadgeClass('order', 'InProgress'),
  },
  {
    statusKey: 'enums.order_status.completed',
    descriptionKey: 'help.orders.status.completed_desc',
    colorClass: legendBadgeClass('order', 'Completed'),
  },
  {
    statusKey: 'enums.order_status.cancelled',
    descriptionKey: 'help.orders.status.cancelled_desc',
    colorClass: legendBadgeClass('order', 'Cancelled'),
  },
];

export const PAYMENT_STATUS_FLOW: StatusFlowItem[] = [
  {
    statusKey: 'enums.payment_status.pending',
    descriptionKey: 'help.orders.payment.pending_desc',
    colorClass: legendBadgeClass('payment', 'Pending'),
  },
  {
    statusKey: 'enums.payment_status.paid',
    descriptionKey: 'help.orders.payment.paid_desc',
    colorClass: legendBadgeClass('payment', 'Paid'),
  },
  {
    statusKey: 'enums.payment_status.failed',
    descriptionKey: 'help.orders.payment.failed_desc',
    colorClass: legendBadgeClass('payment', 'Failed'),
  },
  {
    statusKey: 'enums.payment_status.refunded',
    descriptionKey: 'help.orders.payment.refunded_desc',
    colorClass: legendBadgeClass('payment', 'Refunded'),
  },
];

export function getAvailableOrdersTableDefinition(
  defs: {
    onTakeOrder: (row: OrderListItem) => void;
    isTakeInFlight: (row: OrderListItem) => boolean;
  },
  lang: string | undefined,
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
        getValue: (row?: OrderListItem) => formatDate(row?.cleaningDateTime, lang, 'dateTime'),
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
            ? formatMoney(row.totalPrice, row.currency?.code, localeFor(lang), { fractionDigits: 2 })
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
          // Mirrors OrderAvailability.OfferableStatuses — started is not over, so a job whose first
          // cleaner is already travelling can still be taken while a seat remains.
          const isTakeable =
            status === OrderStatus.New ||
            status === OrderStatus.Confirmed ||
            status === OrderStatus.OnTheWay ||
            status === OrderStatus.InProgress;
          return isTakeable && (row.availableSpots ?? 0) > 0;
        },
        disabled: (row: OrderListItem) => defs.isTakeInFlight(row),
      },
    ],
  };
}

export function getMyOrdersTableDefinition(
  defs: {
    onStartOrder: (row: OrderListItem) => void;
    onCompleteOrder: (row: OrderListItem) => void;
  },
  lang: string | undefined,
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
        getValue: (row?: OrderListItem) => formatDate(row?.cleaningDateTime, lang, 'dateTime'),
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
            ? formatMoney(row.totalPrice, row.currency?.code, localeFor(lang), { fractionDigits: 2 })
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
