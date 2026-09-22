import { ExtraListItem } from '@cleansia/admin-services';
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

export function getExtraTableDefinition(
  defs: {
    onEdit: (row: ExtraListItem) => void;
    onDelete: (row: ExtraListItem) => void;
    onDeactivate: (row: ExtraListItem) => void;
    onActivate: (row: ExtraListItem) => void;
    // ExtraListItem carries no isActive flag, so per-row state is unknown;
    // the toggle visibility is driven by the list's current IsActive filter.
    getIsActiveFilter: () => boolean | undefined;
  },
  translate: TranslateService,
  permissions: PermissionService,
  formatCurrency: (value: number | undefined) => string
): { columns: TableColumn<ExtraListItem>[]; actions: TableAction<ExtraListItem>[] } {
  return {
    columns: [
      {
        id: 'slug',
        field: 'slug',
        header: translate.instant('pages.extra_management.columns.slug'),
        sortable: false,
        width: '20%',
      },
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.extra_management.columns.name'),
        sortable: true,
        width: '30%',
      },
      {
        id: 'price',
        numeric: true,
        field: 'price',
        header: translate.instant('pages.extra_management.columns.price'),
        getValue: (row: ExtraListItem) => formatCurrency(row?.price),
        // A price per currency has no single order, so the backend sort falls
        // through to DisplayOrder for it; an unsortable header does not lie.
        sortable: false,
        width: '20%',
      },
      {
        id: 'displayOrder',
        numeric: true,
        field: 'displayOrder',
        header: translate.instant(
          'pages.extra_management.columns.display_order'
        ),
        sortable: true,
        width: '15%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.extra_management.edit_extra'),
        color: 'warning',
        visible: () => permissions.hasPolicy(Policy.CanUpdateExtra),
        onClick: (row: ExtraListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-ban',
        tooltip: translate.instant('pages.extra_management.deactivate_extra'),
        color: 'danger',
        visible: () =>
          defs.getIsActiveFilter() !== false && permissions.hasPolicy(Policy.CanUpdateExtra),
        onClick: (row: ExtraListItem) => defs.onDeactivate(row),
      },
      {
        icon: 'pi pi-check-circle',
        tooltip: translate.instant('pages.extra_management.activate_extra'),
        color: 'success',
        visible: () =>
          defs.getIsActiveFilter() !== true && permissions.hasPolicy(Policy.CanUpdateExtra),
        onClick: (row: ExtraListItem) => defs.onActivate(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.extra_management.delete_extra'),
        color: 'danger',
        visible: () => permissions.hasPolicy(Policy.CanDeleteExtra),
        onClick: (row: ExtraListItem) => defs.onDelete(row),
      },
    ],
  };
}
