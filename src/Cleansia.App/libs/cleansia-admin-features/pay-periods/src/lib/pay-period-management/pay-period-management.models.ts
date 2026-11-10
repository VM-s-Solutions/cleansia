import { TemplateRef } from '@angular/core';
import { PayPeriodDto } from '@cleansia/admin-services';
import { TableColumn } from '@cleansia/components';
import { formatDate, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export interface PayPeriodFilterParams {
  status?: number;
  year?: number;
}

// ngx-translate has no plurals, so the CLDR rules pick the form and the bundle holds its wording.
export function formatPayPeriodDuration(days: number, translate: TranslateService): string {
  const form = new Intl.PluralRules(localeFor(translate.currentLang)).select(days);
  return translate.instant(`pay_periods.days_${form}`, { count: days });
}

export function formatPayPeriodClosedBy(closedBy: string | undefined, translate: TranslateService): string {
  if (!closedBy) return '-';
  return closedBy === 'System' ? translate.instant('pay_periods.closed_by_system') : closedBy;
}

export function getPayPeriodTableColumns(
  translate: TranslateService,
  statusTemplate?: TemplateRef<PayPeriodDto>
): TableColumn<PayPeriodDto>[] {
  return [
    {
      id: 'startDate',
      numeric: true,
      field: 'startDate',
      header: 'pay_periods.list.columns.start_date',
      sortable: true,
      width: '12%',
      getValue: (row: PayPeriodDto) => formatDate(row.startDate, translate.currentLang, 'utcDate'),
    },
    {
      id: 'endDate',
      numeric: true,
      field: 'endDate',
      header: 'pay_periods.list.columns.end_date',
      sortable: true,
      width: '12%',
      getValue: (row: PayPeriodDto) => formatDate(row.endDate, translate.currentLang, 'utcDate'),
    },
    {
      id: 'durationDays',
      numeric: true,
      field: 'durationDays',
      header: 'pay_periods.list.columns.duration',
      width: '10%',
      getValue: (row: PayPeriodDto) =>
        row.durationDays ? formatPayPeriodDuration(row.durationDays, translate) : '',
    },
    {
      id: 'status',
      field: 'status',
      header: 'pay_periods.list.columns.status',
      sortable: true,
      width: '12%',
      align: 'center',
      customTemplate: statusTemplate,
    },
    {
      id: 'closedAt',
      numeric: true,
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
      getValue: (row: PayPeriodDto) => formatPayPeriodClosedBy(row.closedBy, translate),
    },
  ];
}
