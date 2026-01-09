import { AdminUserListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getAdminUserTableDefinition(
  defs: {
    onEdit: (row: AdminUserListItem) => void;
    onToggleStatus: (row: AdminUserListItem) => void;
  },
  translate: TranslateService
): TableDefinition<AdminUserListItem> {
  return {
    columns: [
      {
        id: 'name',
        headerName: translate.instant('pages.admin_user_management.columns.name'),
        value: (row?: AdminUserListItem) =>
          row ? `${row.firstName ?? ''} ${row.lastName ?? ''}`.trim() : '',
        sortable: true,
        sortField: 'FirstName',
        columnClass: 'width-20',
      },
      {
        id: 'email',
        headerName: translate.instant('pages.admin_user_management.columns.email'),
        value: 'email',
        sortable: true,
        sortField: 'Email',
        columnClass: 'width-25',
      },
      {
        id: 'phone',
        headerName: translate.instant('pages.admin_user_management.columns.phone'),
        value: 'phoneNumber',
        columnClass: 'width-15',
      },
      {
        id: 'status',
        headerName: translate.instant('pages.admin_user_management.columns.status'),
        value: (row?: AdminUserListItem) =>
          row?.isActive
            ? translate.instant('global.status.active')
            : translate.instant('global.status.inactive'),
        columnClass: 'width-10',
      },
      {
        id: 'createdAt',
        headerName: translate.instant('pages.admin_user_management.columns.created_at'),
        value: (row?: AdminUserListItem) =>
          row?.createdAt
            ? new Date(row.createdAt).toLocaleDateString()
            : '',
        sortable: true,
        sortField: 'CreatedOn',
        columnClass: 'width-15',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.admin_user_management.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: AdminUserListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.admin_user_management.edit_user'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-ban',
            onClick: (row: AdminUserListItem) => defs.onToggleStatus(row),
            buttonPalette: 'p-button-danger p-button-sm',
            tooltip: {
              title: translate.instant('pages.admin_user_management.deactivate_user'),
              position: 'above',
            },
            visible: (row: AdminUserListItem) => !!row.isActive,
          },
          {
            icon: 'pi pi-check-circle',
            onClick: (row: AdminUserListItem) => defs.onToggleStatus(row),
            buttonPalette: 'p-button-success p-button-sm',
            tooltip: {
              title: translate.instant('pages.admin_user_management.activate_user'),
              position: 'above',
            },
            visible: (row: AdminUserListItem) => !row.isActive,
          },
        ],
        columnClass: 'width-15',
      },
    ],
  };
}