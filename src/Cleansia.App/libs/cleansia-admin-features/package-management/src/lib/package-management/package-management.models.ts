import { PackageListItem } from '@cleansia/admin-services';
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

export function getPackageTableDefinition(
  defs: {
    onEdit: (row: PackageListItem) => void;
    onDelete: (row: PackageListItem) => void;
    onDeactivate: (row: PackageListItem) => void;
    onActivate: (row: PackageListItem) => void;
    // PackageListItem carries no isActive flag, so per-row state is unknown;
    // the toggle visibility is driven by the list's current IsActive filter.
    getIsActiveFilter: () => boolean | undefined;
  },
  translate: TranslateService,
  permissions: PermissionService,
  formatCurrency: (value: number | undefined) => string
): { columns: TableColumn<PackageListItem>[]; actions: TableAction<PackageListItem>[] } {
  return {
    columns: [
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.package_management.columns.name'),
        sortable: true,
        width: '25%',
      },
      {
        id: 'description',
        field: 'description',
        header: translate.instant(
          'pages.package_management.columns.description'
        ),
        getValue: (row: PackageListItem) => {
          if (!row?.description) return '';
          return row.description.length > 100
            ? row.description.substring(0, 100) + '...'
            : row.description;
        },
        width: '40%',
      },
      {
        id: 'price',
        field: 'price',
        header: translate.instant('pages.package_management.columns.price'),
        getValue: (row: PackageListItem) => formatCurrency(row?.price),
        sortable: true,
        width: '20%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.package_management.edit_package'),
        color: 'warning',
        visible: () => permissions.hasPolicy(Policy.CanUpdatePackage),
        onClick: (row: PackageListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-ban',
        tooltip: translate.instant('pages.package_management.deactivate_package'),
        color: 'danger',
        visible: () =>
          defs.getIsActiveFilter() !== false && permissions.hasPolicy(Policy.CanUpdatePackage),
        onClick: (row: PackageListItem) => defs.onDeactivate(row),
      },
      {
        icon: 'pi pi-check-circle',
        tooltip: translate.instant('pages.package_management.activate_package'),
        color: 'success',
        visible: () =>
          defs.getIsActiveFilter() !== true && permissions.hasPolicy(Policy.CanUpdatePackage),
        onClick: (row: PackageListItem) => defs.onActivate(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.package_management.delete_package'),
        color: 'danger',
        visible: () => permissions.hasPolicy(Policy.CanDeletePackage),
        onClick: (row: PackageListItem) => defs.onDelete(row),
      },
    ],
  };
}