import { PayPeriodDto, PayPeriodStatus } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export interface PayPeriodFilterParams {
  status?: number;
  year?: number;
}

export function getPayPeriodTableDefinition(
  defs: {
    onViewDetails: (row: PayPeriodDto) => void;
    onClose: (row: PayPeriodDto) => void;
  },
  translate: TranslateService
): TableDefinition<PayPeriodDto> {
  return {
    columns: [
      {
        id: 'periodLabel',
        headerName: translate.instant('payPeriods.list.columns.periodLabel'),
        value: 'periodLabel',
        sortable: true,
        columnClass: 'width-15',
      },
      {
        id: 'startDate',
        headerName: translate.instant('payPeriods.list.columns.startDate'),
        value: (row?: PayPeriodDto) => {
          if (!row?.startDate) return '';
          const date = new Date(row.startDate);
          return date.toLocaleDateString('cs-CZ');
        },
        sortable: true,
        sortField: 'startDate',
        columnClass: 'width-12',
      },
      {
        id: 'endDate',
        headerName: translate.instant('payPeriods.list.columns.endDate'),
        value: (row?: PayPeriodDto) => {
          if (!row?.endDate) return '';
          const date = new Date(row.endDate);
          return date.toLocaleDateString('cs-CZ');
        },
        sortable: true,
        sortField: 'endDate',
        columnClass: 'width-12',
      },
      {
        id: 'durationDays',
        headerName: translate.instant('payPeriods.list.columns.duration'),
        value: (row?: PayPeriodDto) =>
          row?.durationDays
            ? `${row.durationDays} ${translate.instant('payPeriods.list.days')}`
            : '',
        columnClass: 'width-10',
      },
      {
        id: 'status',
        headerName: translate.instant('payPeriods.list.columns.status'),
        value: (row?: PayPeriodDto) =>
          row?.status
            ? translate.instant(`payPeriods.status.${row.status.toLowerCase()}`)
            : '',
        sortable: true,
        sortField: 'status',
        columnClass: 'width-12',
      },
      {
        id: 'closedAt',
        headerName: translate.instant('payPeriods.list.columns.closedAt'),
        value: (row?: PayPeriodDto) => {
          if (!row?.closedAt) return '-';
          const date =
            row.closedAt instanceof Date
              ? row.closedAt
              : new Date(row.closedAt);
          return date.toLocaleDateString('cs-CZ');
        },
        columnClass: 'width-12',
      },
      {
        id: 'closedBy',
        headerName: translate.instant('payPeriods.list.columns.closedBy'),
        value: (row?: PayPeriodDto) => row?.closedBy || '-',
        columnClass: 'width-12',
      },
      {
        id: 'actions',
        headerName: translate.instant('payPeriods.list.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-eye',
            onClick: (row: PayPeriodDto) => defs.onViewDetails(row),
            buttonPalette: 'p-button-info p-button-sm',
            tooltip: {
              title: translate.instant('payPeriods.list.viewDetails'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-lock',
            onClick: (row: PayPeriodDto) => defs.onClose(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('payPeriods.list.closePeriod'),
              position: 'above',
            },
            visible: (row: PayPeriodDto) =>
              row.status === PayPeriodStatus[PayPeriodStatus.Open],
          },
        ],
        columnClass: 'width-15',
      },
    ],
  };
}
