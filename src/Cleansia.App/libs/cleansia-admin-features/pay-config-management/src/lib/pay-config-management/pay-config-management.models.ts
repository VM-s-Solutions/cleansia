import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export interface PayConfigListItem {
  id?: string;
  serviceId?: string;
  serviceName?: string;
  packageId?: string;
  packageName?: string;
  basePay?: number;
  extraPerRoom?: number;
  extraPerBathroom?: number;
  distanceRatePerKm?: number;
  minimumPay?: number;
  maximumPay?: number;
  currencyId?: string;
  currencyCode?: string;
  description?: string;
  createdOn?: Date;
}

export type GradeLevel = 'junior' | 'medior' | 'senior';

export const GRADE_MULTIPLIERS: Record<GradeLevel, number> = {
  junior: 0.5,
  medior: 0.75,
  senior: 1.0,
};

export function getPayConfigTableDefinition(
  defs: {
    onEdit: (row: PayConfigListItem) => void;
    onDelete: (row: PayConfigListItem) => void;
  },
  translate: TranslateService,
  formatCurrency: (value: number | undefined) => string
): { columns: TableColumn<PayConfigListItem>[]; actions: TableAction<PayConfigListItem>[] } {
  return {
    columns: [
      {
        id: 'serviceName',
        field: 'serviceName',
        header: translate.instant('pages.pay_config_management.columns.service'),
        getValue: (row: PayConfigListItem) => row?.serviceName || row?.packageName || '-',
        sortable: true,
        width: '20%',
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: translate.instant('pages.pay_config_management.columns.base_pay'),
        getValue: (row: PayConfigListItem) => formatCurrency(row?.basePay),
        sortable: true,
        width: '15%',
      },
      {
        id: 'extraPerRoom',
        field: 'extraPerRoom',
        header: translate.instant('pages.pay_config_management.columns.per_room'),
        getValue: (row: PayConfigListItem) => formatCurrency(row?.extraPerRoom),
        sortable: true,
        width: '15%',
      },
      {
        id: 'extraPerBathroom',
        field: 'extraPerBathroom',
        header: translate.instant('pages.pay_config_management.columns.per_bathroom'),
        getValue: (row: PayConfigListItem) => formatCurrency(row?.extraPerBathroom),
        sortable: true,
        width: '15%',
      },
      {
        id: 'description',
        field: 'description',
        header: translate.instant('pages.pay_config_management.columns.description'),
        getValue: (row: PayConfigListItem) => {
          if (!row?.description) return '';
          return row.description.length > 80
            ? row.description.substring(0, 80) + '...'
            : row.description;
        },
        width: '20%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.pay_config_management.edit'),
        color: 'warning',
        onClick: (row: PayConfigListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.pay_config_management.delete'),
        color: 'danger',
        onClick: (row: PayConfigListItem) => defs.onDelete(row),
      },
    ],
  };
}
