import { TemplateRef } from '@angular/core';
import { AdminUserListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getAdminUserTableDefinition(
  defs: {
    onEdit: (row: AdminUserListItem) => void;
    onToggleStatus: (row: AdminUserListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<AdminUserListItem>
): { columns: TableColumn<AdminUserListItem>[]; actions: TableAction<AdminUserListItem>[] } {
  return {
    columns: [
      {
        id: 'name',
        field: 'firstName',
        header: translate.instant('pages.admin_user_management.columns.name'),
        getValue: (row: AdminUserListItem) =>
          `${row.firstName ?? ''} ${row.lastName ?? ''}`.trim(),
        sortable: true,
        width: '20%',
      },
      {
        id: 'email',
        field: 'email',
        header: translate.instant('pages.admin_user_management.columns.email'),
        sortable: true,
        width: '25%',
      },
      {
        id: 'phone',
        field: 'phoneNumber',
        header: translate.instant('pages.admin_user_management.columns.phone'),
        width: '15%',
      },
      {
        id: 'status',
        field: 'isActive',
        header: translate.instant('pages.admin_user_management.columns.status'),
        customTemplate: statusTemplate,
        width: '10%',
      },
      {
        id: 'createdAt',
        field: 'createdAt',
        header: translate.instant('pages.admin_user_management.columns.created_at'),
        getValue: (row: AdminUserListItem) =>
          row.createdAt
            ? new Date(row.createdAt).toLocaleDateString()
            : '',
        sortable: true,
        width: '15%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.admin_user_management.edit_user'),
        color: 'warning',
        onClick: (row: AdminUserListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-ban',
        tooltip: translate.instant('pages.admin_user_management.deactivate_user'),
        color: 'danger',
        onClick: (row: AdminUserListItem) => defs.onToggleStatus(row),
        visible: (row: AdminUserListItem) => !!row.isActive,
      },
      {
        icon: 'pi pi-check-circle',
        tooltip: translate.instant('pages.admin_user_management.activate_user'),
        color: 'success',
        onClick: (row: AdminUserListItem) => defs.onToggleStatus(row),
        visible: (row: AdminUserListItem) => !row.isActive,
      },
    ],
  };
}