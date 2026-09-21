import { TemplateRef } from '@angular/core';
import { PayPeriodDto, PayPeriodStatus } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { formatDate } from '@cleansia/utils';
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
      header: 'pay_periods.list.columns.period_label',
      sortable: true,
      width: '15%',
    },
    {
      id: 'startDate',
      field: 'startDate',
      header: 'pay_periods.list.columns.start_date',
      sortable: true,
      width: '12%',
      getValue: (row: PayPeriodDto) => formatDate(row.startDate, translate.currentLang),
    },
    {
      id: 'endDate',
      field: 'endDate',
      header: 'pay_periods.list.columns.end_date',
      sortable: true,
      width: '12%',
      getValue: (row: PayPeriodDto) => formatDate(row.endDate, translate.currentLang),
    },
    {
      id: 'durationDays',
      field: 'durationDays',
      header: 'pay_periods.list.columns.duration',
      width: '10%',
      getValue: (row: PayPeriodDto) =>
        row.durationDays
          ? `${row.durationDays} ${translate.instant('pay_periods.list.days')}`
          : '',
    },
    {
      id: 'status',
      field: 'status',
      header: 'pay_periods.list.columns.status',
      sortable: true,
      width: '12%',
      customTemplate: statusTemplate,
    },
    {
      id: 'closedAt',
      field: 'closedAt',
      header: 'pay_periods.list.columns.closed_at',
      width: '12%',
      getValue: (row: PayPeriodDto) => formatDate(row.closedAt, translate.currentLang) || '-',
    },
    {
      id: 'closedBy',
      field: 'closedBy',
      header: 'pay_periods.list.columns.closed_by',
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
      tooltip: translate.instant('pay_periods.list.view_details'),
    },
    {
      icon: 'pi pi-lock',
      onClick: (row: PayPeriodDto) => defs.onClose(row),
      color: 'warning',
      tooltip: translate.instant('pay_periods.list.close_period'),
      visible: (row: PayPeriodDto) =>
        row.status === PayPeriodStatus[PayPeriodStatus.Open],
    },
  ];
}
