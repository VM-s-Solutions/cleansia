import { TemplateRef } from '@angular/core';
import { TableDefinition } from '@cleansia/components';
import { OrderListItem } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

export function getOrderTableDefinition(
  defs: {
    onViewDetails: (row: OrderListItem) => void;
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
        columnClass: 'width-15',
      },
      {
        id: 'customerName',
        headerName: translate.instant('pages.orders.customer_name'),
        value: 'customerName',
        sortable: true,
        columnClass: 'width-20',
      },
      {
        id: 'customerEmail',
        headerName: translate.instant('pages.orders.customer_email'),
        value: 'customerEmail',
        columnClass: 'width-20',
      },
      {
        id: 'customerPhone',
        headerName: translate.instant('pages.orders.customer_phone'),
        value: 'customerPhone',
        columnClass: 'width-15',
      },
      {
        id: 'cleaningDateTime',
        headerName: translate.instant('pages.orders.cleaning_date'),
        value: (row?: OrderListItem) =>
          row?.cleaningDateTime
            ? new Date(row.cleaningDateTime).toLocaleDateString('cs-CZ')
            : '',
        sortable: true,
        columnClass: 'width-15',
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
        columnClass: 'width-15',
      },
      {
        id: 'paymentStatus',
        headerName: translate.instant('pages.orders.payment_status'),
        template: statusTemplate,
        columnClass: 'width-15',
      },
      {
        id: 'orderStatus',
        headerName: translate.instant('pages.orders.order_status'),
        template: orderStatusTemplate,
        columnClass: 'width-15',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.orders.actions'),
        columnActions: [
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

