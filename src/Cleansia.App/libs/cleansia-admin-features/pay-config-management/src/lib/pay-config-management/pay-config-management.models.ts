import { EmployeePayConfigDto } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

export function getPayConfigTableDefinition(
  defs: {
    onEdit: (row: EmployeePayConfigDto) => void;
    onDelete: (row: EmployeePayConfigDto) => void;
  },
  translate: TranslateService,
  permissions: PermissionService,
  formatCurrency: (value: number | undefined, currencyCode?: string) => string
): { columns: TableColumn<EmployeePayConfigDto>[]; actions: TableAction<EmployeePayConfigDto>[] } {
  return {
    columns: [
      {
        id: 'serviceName',
        field: 'serviceName',
        header: translate.instant('pages.pay_config_management.columns.service'),
        getValue: (row: EmployeePayConfigDto) => row?.serviceName || row?.packageName || '-',
        sortable: true,
        width: '18%',
      },
      {
        id: 'basePay',
        numeric: true,
        field: 'basePay',
        header: translate.instant('pages.pay_config_management.columns.base_pay'),
        getValue: (row: EmployeePayConfigDto) =>
          formatCurrency(row?.basePay, row?.currencyCode),
        sortable: true,
        width: '13%',
      },
      {
        id: 'extraPerRoom',
        numeric: true,
        field: 'extraPerRoom',
        header: translate.instant('pages.pay_config_management.columns.per_room'),
        getValue: (row: EmployeePayConfigDto) =>
          formatCurrency(row?.extraPerRoom, row?.currencyCode),
        sortable: true,
        width: '13%',
      },
      {
        id: 'extraPerBathroom',
        numeric: true,
        field: 'extraPerBathroom',
        header: translate.instant('pages.pay_config_management.columns.per_bathroom'),
        getValue: (row: EmployeePayConfigDto) =>
          formatCurrency(row?.extraPerBathroom, row?.currencyCode),
        sortable: true,
        width: '13%',
      },
      {
        id: 'description',
        field: 'description',
        header: translate.instant('pages.pay_config_management.columns.description'),
        getValue: (row: EmployeePayConfigDto) => row?.description ?? '',
        width: '27%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.pay_config_management.edit'),
        color: 'warning',
        visible: () => permissions.hasPolicy(Policy.CanUpdatePayConfig),
        onClick: (row: EmployeePayConfigDto) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.pay_config_management.delete'),
        color: 'danger',
        visible: () => permissions.hasPolicy(Policy.CanDeletePayConfig),
        onClick: (row: EmployeePayConfigDto) => defs.onDelete(row),
      },
    ],
  };
}
