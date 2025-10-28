import { TemplateRef } from '@angular/core';
import { TableDefinition } from '@cleansia/components';
import { OrderListItem, OrderStatus } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

export function getAvailableOrdersTableDefinition(
  defs: {
    onViewDetails: (row: OrderListItem) => void;
    onTakeOrder: (row: OrderListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<OrderListItem>,
  orderStatusTemplate?: TemplateRef<OrderListItem>
): TableDefinition<OrderListItem> {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        headerName: translate.instant('pages.orders.order_number'),
        value: 'displayOrderNumber',
        sortable: true,
        columnClass: 'width-12',
      },
      {
        id: 'cleaningDateTime',
        headerName: translate.instant('pages.orders.cleaning_date'),
        value: (row?: OrderListItem) =>
          row?.cleaningDateTime
            ? new Date(row.cleaningDateTime).toLocaleDateString('cs-CZ')
            : '',
        sortable: true,
        columnClass: 'width-12',
      },
      {
        id: 'address',
        headerName: translate.instant('pages.orders.address'),
        value: (row?: OrderListItem) =>
          `${row?.customerAddress || ''}`.trim().replace(/^,\s*/, ''),
        columnClass: 'width-20',
      },
      {
        id: 'totalPrice',
        headerName: translate.instant('pages.orders.total_price'),
        value: (row?: OrderListItem) =>
          row?.totalPrice
            ? Number(row.totalPrice).toLocaleString('cs-CZ', {
                style: 'currency',
                currency: 'CZK',
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
              })
            : '',
        sortable: true,
        columnClass: 'width-12',
      },
      {
        id: 'availableSpots',
        headerName: translate.instant('pages.orders.available_spots'),
        value: (row?: OrderListItem) =>
          `${row?.availableSpots || 0} / ${row?.maxEmployees || 0}`,
        columnClass: 'width-10',
      },
      {
        id: 'paymentStatus',
        headerName: translate.instant('pages.orders.payment_status'),
        template: statusTemplate,
        columnClass: 'width-12',
      },
      {
        id: 'orderStatus',
        headerName: translate.instant('pages.orders.order_status'),
        template: orderStatusTemplate,
        columnClass: 'width-12',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.orders.actions'),
        columnActions: [
          {
            icon: 'pi pi-check',
            onClick: (row: OrderListItem) => defs.onTakeOrder(row),
            buttonPalette: 'p-button-success p-button-sm',
            tooltip: {
              title: translate.instant('pages.orders.take_order'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-eye',
            onClick: (row: OrderListItem) => defs.onViewDetails(row),
            buttonPalette: 'p-button-outlined p-button-sm',
            tooltip: {
              title: translate.instant('pages.orders.view_details'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-15',
      },
    ],
  };
}

export function getMyOrdersTableDefinition(
  defs: {
    onViewDetails: (row: OrderListItem) => void;
    onCompleteOrder: (row: OrderListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<OrderListItem>,
  orderStatusTemplate?: TemplateRef<OrderListItem>
): TableDefinition<OrderListItem> {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        headerName: translate.instant('pages.orders.order_number'),
        value: 'displayOrderNumber',
        sortable: true,
        columnClass: 'width-12',
      },
      {
        id: 'customerName',
        headerName: translate.instant('pages.orders.customer_name'),
        value: 'customerName',
        sortable: true,
        columnClass: 'width-15',
      },
      {
        id: 'customerPhone',
        headerName: translate.instant('pages.orders.customer_phone'),
        value: 'customerPhone',
        columnClass: 'width-12',
      },
      {
        id: 'cleaningDateTime',
        headerName: translate.instant('pages.orders.cleaning_date'),
        value: (row?: OrderListItem) =>
          row?.cleaningDateTime
            ? new Date(row.cleaningDateTime).toLocaleDateString('cs-CZ')
            : '',
        sortable: true,
        columnClass: 'width-12',
      },
      {
        id: 'address',
        headerName: translate.instant('pages.orders.address'),
        value: (row?: OrderListItem) =>
          `${row?.customerAddress || ''}`.trim().replace(/^,\s*/, ''),
        columnClass: 'width-18',
      },
      {
        id: 'totalPrice',
        headerName: translate.instant('pages.orders.total_price'),
        value: (row?: OrderListItem) =>
          row?.totalPrice
            ? Number(row.totalPrice).toLocaleString('cs-CZ', {
                style: 'currency',
                currency: 'CZK',
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
              })
            : '',
        sortable: true,
        columnClass: 'width-12',
      },
      {
        id: 'orderStatus',
        headerName: translate.instant('pages.orders.order_status'),
        template: orderStatusTemplate,
        columnClass: 'width-12',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.orders.actions'),
        columnActions: [
          {
            icon: 'pi pi-check-circle',
            onClick: (row: OrderListItem) => defs.onCompleteOrder(row),
            buttonPalette: 'p-button-success p-button-sm',
            tooltip: {
              title: translate.instant('pages.orders.complete_order.title'),
              position: 'above',
            },
            visible: (row: OrderListItem) =>
              row.orderStatus.value === OrderStatus.InProgress, // InProgress
          },
          {
            icon: 'pi pi-eye',
            onClick: (row: OrderListItem) => defs.onViewDetails(row),
            buttonPalette: 'p-button-outlined p-button-sm',
            tooltip: {
              title: translate.instant('pages.orders.view_details'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-12',
      },
    ],
  };
}
