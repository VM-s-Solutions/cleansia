import { TemplateRef } from '@angular/core';
import {
  AdminEmployeeListItem,
  ContractStatus,
} from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getEmployeeTableDefinition(
  defs: {
    onApprove: (row: AdminEmployeeListItem) => void;
    onReject: (row: AdminEmployeeListItem) => void;
    onViewDetails: (row: AdminEmployeeListItem) => void;
  },
  translate: TranslateService,
  contractStatusTemplate?: TemplateRef<AdminEmployeeListItem>
): TableDefinition<AdminEmployeeListItem> {
  return {
    columns: [
      {
        id: 'name',
        headerName: translate.instant('pages.employee_management.name'),
        value: (row?: AdminEmployeeListItem) =>
          row ? `${row.firstName || ''} ${row.lastName || ''}`.trim() : '',
        sortable: true,
        sortField: 'firstName',
        columnClass: 'width-15',
      },
      {
        id: 'email',
        headerName: translate.instant('pages.employee_management.email'),
        value: 'email',
        sortable: true,
        columnClass: 'width-15',
      },
      {
        id: 'phoneNumber',
        headerName: translate.instant('pages.employee_management.phone'),
        value: (row?: AdminEmployeeListItem) => row?.phoneNumber || '-',
        columnClass: 'width-12',
      },
      {
        id: 'nationalityName',
        headerName: translate.instant('pages.employee_management.nationality'),
        value: (row?: AdminEmployeeListItem) => row?.nationalityName || '-',
        columnClass: 'width-12',
      },
      {
        id: 'contractStatus',
        headerName: translate.instant('pages.employee_management.status'),
        template: contractStatusTemplate,
        sortable: true,
        sortField: 'contractStatus',
        columnClass: 'width-12',
      },
      {
        id: 'averageRating',
        headerName: translate.instant('pages.employee_management.rating'),
        value: (row?: AdminEmployeeListItem) =>
          row?.averageRating?.toFixed(1) || '0.0',
        columnClass: 'width-8',
      },
      {
        id: 'complaintsCount',
        headerName: translate.instant('pages.employee_management.complaints'),
        value: (row?: AdminEmployeeListItem) =>
          row?.complaintsCount?.toString() || '0',
        columnClass: 'width-8',
      },
      {
        id: 'createdAt',
        headerName: translate.instant('pages.employee_management.created_at'),
        value: (row?: AdminEmployeeListItem) => {
          if (!row?.createdAt) return '';
          const date =
            row.createdAt instanceof Date
              ? row.createdAt
              : new Date(row.createdAt);
          return date.toLocaleDateString('cs-CZ');
        },
        sortable: true,
        sortField: 'createdAt',
        columnClass: 'width-12',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.employee_management.actions'),
        columnActions: [
          {
            icon: 'pi pi-eye',
            onClick: (row: AdminEmployeeListItem) => defs.onViewDetails(row),
            buttonPalette: 'p-button-info p-button-sm',
            tooltip: {
              title: translate.instant(
                'pages.employee_management.view_details'
              ),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-check',
            onClick: (row: AdminEmployeeListItem) => defs.onApprove(row),
            buttonPalette: 'p-button-success p-button-sm',
            tooltip: {
              title: translate.instant('pages.employee_management.approve'),
              position: 'above',
            },
            visible: (row: AdminEmployeeListItem) =>
              row.contractStatus === ContractStatus[ContractStatus.Pending] &&
              row.isProfileComplete,
          },
          {
            icon: 'pi pi-times',
            onClick: (row: AdminEmployeeListItem) => defs.onReject(row),
            buttonPalette: 'p-button-danger p-button-sm',
            tooltip: {
              title: translate.instant('pages.employee_management.reject'),
              position: 'above',
            },
            visible: (row: AdminEmployeeListItem) =>
              row.contractStatus === ContractStatus[ContractStatus.Pending] &&
              row.isProfileComplete,
          },
        ],
        columnClass: 'width-15',
      },
    ],
  };
}
