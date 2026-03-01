import { TemplateRef } from '@angular/core';
import {
  AdminEmployeeListItem,
  ContractStatus,
} from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getEmployeeTableDefinition(
  defs: {
    onApprove: (row: AdminEmployeeListItem) => void;
    onReject: (row: AdminEmployeeListItem) => void;
    onViewDetails: (row: AdminEmployeeListItem) => void;
  },
  translate: TranslateService,
  contractStatusTemplate?: TemplateRef<AdminEmployeeListItem>
): { columns: TableColumn<AdminEmployeeListItem>[]; actions: TableAction<AdminEmployeeListItem>[] } {
  return {
    columns: [
      {
        id: 'name',
        field: 'firstName',
        header: translate.instant('pages.employee_management.name'),
        getValue: (row: AdminEmployeeListItem) =>
          `${row.firstName || ''} ${row.lastName || ''}`.trim(),
        sortable: true,
        width: '15%',
      },
      {
        id: 'email',
        field: 'email',
        header: translate.instant('pages.employee_management.email'),
        sortable: true,
        width: '15%',
      },
      {
        id: 'phoneNumber',
        field: 'phoneNumber',
        header: translate.instant('pages.employee_management.phone'),
        getValue: (row: AdminEmployeeListItem) => row.phoneNumber || '-',
        width: '12%',
      },
      {
        id: 'nationalityName',
        field: 'nationalityName',
        header: translate.instant('pages.employee_management.nationality'),
        getValue: (row: AdminEmployeeListItem) => row.nationalityName || '-',
        width: '12%',
      },
      {
        id: 'contractStatus',
        field: 'contractStatus',
        header: translate.instant('pages.employee_management.status'),
        customTemplate: contractStatusTemplate,
        sortable: true,
        width: '12%',
      },
      {
        id: 'averageRating',
        field: 'averageRating',
        header: translate.instant('pages.employee_management.rating'),
        getValue: (row: AdminEmployeeListItem) =>
          row.averageRating?.toFixed(1) || '0.0',
        width: '8%',
      },
      {
        id: 'complaintsCount',
        field: 'complaintsCount',
        header: translate.instant('pages.employee_management.complaints'),
        getValue: (row: AdminEmployeeListItem) =>
          row.complaintsCount?.toString() || '0',
        width: '8%',
      },
      {
        id: 'createdAt',
        field: 'createdAt',
        header: translate.instant('pages.employee_management.created_at'),
        getValue: (row: AdminEmployeeListItem) => {
          if (!row.createdAt) return '';
          const date =
            row.createdAt instanceof Date
              ? row.createdAt
              : new Date(row.createdAt);
          return date.toLocaleDateString('en-GB');
        },
        sortable: true,
        width: '12%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.employee_management.view_details'),
        color: 'info',
        onClick: (row: AdminEmployeeListItem) => defs.onViewDetails(row),
      },
      {
        icon: 'pi pi-check',
        tooltip: translate.instant('pages.employee_management.approve'),
        color: 'success',
        onClick: (row: AdminEmployeeListItem) => defs.onApprove(row),
        visible: (row: AdminEmployeeListItem) =>
          row.contractStatus === ContractStatus[ContractStatus.Pending] &&
          row.isProfileComplete,
      },
      {
        icon: 'pi pi-times',
        tooltip: translate.instant('pages.employee_management.reject'),
        color: 'danger',
        onClick: (row: AdminEmployeeListItem) => defs.onReject(row),
        visible: (row: AdminEmployeeListItem) =>
          row.contractStatus === ContractStatus[ContractStatus.Pending] &&
          row.isProfileComplete,
      },
    ],
  };
}
