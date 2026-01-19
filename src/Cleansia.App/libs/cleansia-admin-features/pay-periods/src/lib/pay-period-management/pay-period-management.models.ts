import { TemplateRef } from '@angular/core';
import { PayPeriodDto, PayPeriodStatus } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export interface PayPeriodFilterParams {
  status?: number;
  year?: number;
}

export function getPayPeriodTableColumns(
  translate: TranslateService,
  statusTemplate?: TemplateRef<PayPeriodDto>
): TableColumn<PayPeriodDto>[] {
  return [
    {
      id: 'periodLabel',
      field: 'periodLabel',
      header: 'payPeriods.list.columns.periodLabel',
      sortable: true,
      width: '15%',
    },
    {
      id: 'startDate',
      field: 'startDate',
      header: 'payPeriods.list.columns.startDate',
      sortable: true,
      width: '12%',
      getValue: (row: PayPeriodDto) => {
        if (!row.startDate) return '';
        const date = new Date(row.startDate);
        return date.toLocaleDateString('cs-CZ');
      },
    },
    {
      id: 'endDate',
      field: 'endDate',
      header: 'payPeriods.list.columns.endDate',
      sortable: true,
      width: '12%',
      getValue: (row: PayPeriodDto) => {
        if (!row.endDate) return '';
        const date = new Date(row.endDate);
        return date.toLocaleDateString('cs-CZ');
      },
    },
    {
      id: 'durationDays',
      field: 'durationDays',
      header: 'payPeriods.list.columns.duration',
      width: '10%',
      getValue: (row: PayPeriodDto) =>
        row.durationDays
          ? `${row.durationDays} ${translate.instant('payPeriods.list.days')}`
          : '',
    },
    {
      id: 'status',
      field: 'status',
      header: 'payPeriods.list.columns.status',
      sortable: true,
      width: '12%',
      customTemplate: statusTemplate,
    },
    {
      id: 'closedAt',
      field: 'closedAt',
      header: 'payPeriods.list.columns.closedAt',
      width: '12%',
      getValue: (row: PayPeriodDto) => {
        if (!row.closedAt) return '-';
        const date =
          row.closedAt instanceof Date
            ? row.closedAt
            : new Date(row.closedAt);
        return date.toLocaleDateString('cs-CZ');
      },
    },
    {
      id: 'closedBy',
      field: 'closedBy',
      header: 'payPeriods.list.columns.closedBy',
      width: '12%',
      getValue: (row: PayPeriodDto) => row.closedBy || '-',
    },
  ];
}

export function getPayPeriodTableActions(
  defs: {
    onViewDetails: (row: PayPeriodDto) => void;
    onClose: (row: PayPeriodDto) => void;
  },
  translate: TranslateService
): TableAction<PayPeriodDto>[] {
  return [
    {
      icon: 'pi pi-eye',
      onClick: (row: PayPeriodDto) => defs.onViewDetails(row),
      color: 'info',
      tooltip: translate.instant('payPeriods.list.viewDetails'),
    },
    {
      icon: 'pi pi-lock',
      onClick: (row: PayPeriodDto) => defs.onClose(row),
      color: 'warning',
      tooltip: translate.instant('payPeriods.list.closePeriod'),
      visible: (row: PayPeriodDto) =>
        row.status === PayPeriodStatus[PayPeriodStatus.Open],
    },
  ];
}
