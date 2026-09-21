import { TemplateRef } from '@angular/core';
import { OrderListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';

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
        getValue: (row: OrderListItem) =>
          formatDate(row?.cleaningDateTime, translate.currentLang, 'dateTime'),
      },
      {
        id: 'totalPrice',
        field: 'totalPrice',
        header: translate.instant('pages.order_management.total_price'),
        sortable: true,
        width: '10%',
        getValue: (row: OrderListItem) =>
          row?.totalPrice == null
            ? ''
            : formatMoney(row.totalPrice, row.currency?.code, localeFor(translate.currentLang), {
                fractionDigits: 2,
              }),
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
