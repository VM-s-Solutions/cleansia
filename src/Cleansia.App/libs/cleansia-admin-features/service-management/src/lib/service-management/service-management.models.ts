import { ServiceListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

export type CatalogStatusFilter = 'all' | 'active' | 'inactive';

export function mapStatusFilterToIsActive(
  value: CatalogStatusFilter
): boolean | undefined {
  if (value === 'active') return true;
  if (value === 'inactive') return false;
  return undefined;
}

export function getServiceTableDefinition(
  defs: {
    onEdit: (row: ServiceListItem) => void;
    onDelete: (row: ServiceListItem) => void;
    onDeactivate: (row: ServiceListItem) => void;
    onActivate: (row: ServiceListItem) => void;
    // ServiceListItem carries no isActive flag, so per-row state is unknown;
    // the toggle visibility is driven by the list's current IsActive filter.
    getIsActiveFilter: () => boolean | undefined;
  },
  translate: TranslateService,
  permissions: PermissionService,
  formatCurrency: (value: number | undefined) => string
): { columns: TableColumn<ServiceListItem>[]; actions: TableAction<ServiceListItem>[] } {
  return {
    columns: [
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.service_management.columns.name'),
        sortable: true,
        width: '20%',
      },
      {
        id: 'description',
        field: 'description',
        header: translate.instant(
          'pages.service_management.columns.description'
        ),
        getValue: (row: ServiceListItem) => {
          if (!row?.description) return '';
          return row.description.length > 100
            ? row.description.substring(0, 100) + '...'
            : row.description;
        },
        width: '30%',
      },
      {
        id: 'basePrice',
        numeric: true,
        field: 'basePrice',
        header: translate.instant(
          'pages.service_management.columns.base_price'
        ),
        getValue: (row: ServiceListItem) => formatCurrency(row?.basePrice),
        sortable: true,
        width: '20%',
      },
      {
        id: 'perRoomPrice',
        numeric: true,
        field: 'perRoomPrice',
        header: translate.instant(
          'pages.service_management.columns.per_room_price'
        ),
        getValue: (row: ServiceListItem) => formatCurrency(row?.perRoomPrice),
        sortable: true,
        width: '20%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.service_management.edit_service'),
        color: 'warning',
        visible: () => permissions.hasPolicy(Policy.CanUpdateService),
        onClick: (row: ServiceListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-ban',
        tooltip: translate.instant('pages.service_management.deactivate_service'),
        color: 'danger',
        visible: () =>
          defs.getIsActiveFilter() !== false && permissions.hasPolicy(Policy.CanUpdateService),
        onClick: (row: ServiceListItem) => defs.onDeactivate(row),
      },
      {
        icon: 'pi pi-check-circle',
        tooltip: translate.instant('pages.service_management.activate_service'),
        color: 'success',
        visible: () =>
          defs.getIsActiveFilter() !== true && permissions.hasPolicy(Policy.CanUpdateService),
        onClick: (row: ServiceListItem) => defs.onActivate(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.service_management.delete_service'),
        color: 'danger',
        visible: () => permissions.hasPolicy(Policy.CanDeleteService),
        onClick: (row: ServiceListItem) => defs.onDelete(row),
      },
    ],
  };
}
