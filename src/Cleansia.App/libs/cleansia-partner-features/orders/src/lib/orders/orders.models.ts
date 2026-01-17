import { TemplateRef } from '@angular/core';
import { TableAction, TableColumn } from '@cleansia/components';
import { OrderListItem, OrderStatus } from '@cleansia/partner-services';

export function getAvailableOrdersTableDefinition(
  defs: {
    onViewDetails: (row: OrderListItem) => void;
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
            ? new Date(row.cleaningDateTime).toLocaleDateString('cs-CZ')
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
            ? Number(row.totalPrice).toLocaleString('cs-CZ', {
                style: 'currency',
                currency: 'CZK',
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
      },
      {
        icon: 'pi pi-eye',
        tooltip: 'pages.orders.view_details',
        onClick: (row: OrderListItem) => defs.onViewDetails(row),
      },
    ],
  };
}

export function getMyOrdersTableDefinition(
  defs: {
    onViewDetails: (row: OrderListItem) => void;
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
            ? new Date(row.cleaningDateTime).toLocaleDateString('cs-CZ')
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
            ? Number(row.totalPrice).toLocaleString('cs-CZ', {
                style: 'currency',
                currency: 'CZK',
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
        icon: 'pi pi-check-circle',
        tooltip: 'pages.orders.complete_order.title',
        color: 'success',
        onClick: (row: OrderListItem) => defs.onCompleteOrder(row),
        visible: (row: OrderListItem) =>
          row.orderStatus.value === OrderStatus.InProgress,
      },
      {
        icon: 'pi pi-eye',
        tooltip: 'pages.orders.view_details',
        onClick: (row: OrderListItem) => defs.onViewDetails(row),
      },
    ],
  };
}
