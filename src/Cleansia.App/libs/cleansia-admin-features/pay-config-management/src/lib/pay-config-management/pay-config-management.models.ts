import { EmployeePayConfigDto } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getPayConfigTableDefinition(
  defs: {
    onEdit: (row: EmployeePayConfigDto) => void;
    onDelete: (row: EmployeePayConfigDto) => void;
  },
  translate: TranslateService,
  formatCurrency: (value: number | undefined) => string
): { columns: TableColumn<EmployeePayConfigDto>[]; actions: TableAction<EmployeePayConfigDto>[] } {
  return {
    columns: [
      {
        id: 'serviceName',
        field: 'serviceName',
        header: translate.instant('pages.pay_config_management.columns.service'),
        getValue: (row: EmployeePayConfigDto) => row?.serviceName || row?.packageName || '-',
        sortable: true,
        width: '20%',
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: translate.instant('pages.pay_config_management.columns.base_pay'),
        getValue: (row: EmployeePayConfigDto) => formatCurrency(row?.basePay),
        sortable: true,
        width: '15%',
      },
      {
        id: 'extraPerRoom',
        field: 'extraPerRoom',
        header: translate.instant('pages.pay_config_management.columns.per_room'),
        getValue: (row: EmployeePayConfigDto) => formatCurrency(row?.extraPerRoom),
        sortable: true,
        width: '15%',
      },
      {
        id: 'extraPerBathroom',
        field: 'extraPerBathroom',
        header: translate.instant('pages.pay_config_management.columns.per_bathroom'),
        getValue: (row: EmployeePayConfigDto) => formatCurrency(row?.extraPerBathroom),
        sortable: true,
        width: '15%',
      },
      {
        id: 'description',
        field: 'description',
        header: translate.instant('pages.pay_config_management.columns.description'),
        getValue: (row: EmployeePayConfigDto) => {
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
        onClick: (row: EmployeePayConfigDto) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.pay_config_management.delete'),
        color: 'danger',
        onClick: (row: EmployeePayConfigDto) => defs.onDelete(row),
      },
    ],
  };
}
