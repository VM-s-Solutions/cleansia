import { PackageListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export type CatalogStatusFilter = 'all' | 'active' | 'inactive';

export function mapStatusFilterToIsActive(
  value: CatalogStatusFilter
): boolean | undefined {
  if (value === 'active') return true;
  if (value === 'inactive') return false;
  return undefined;
}

export const PACKAGE_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'package.not_found': 'errors.package.not_found',
  'package.in_use': 'errors.package.in_use',
};

export const PACKAGE_FALLBACK_ERROR_KEY = 'errors.common.error_occurred';

export function resolvePackageErrorKey(error: unknown): string {
  const apiError = error as {
    result?: { detail?: string; title?: string };
    response?: string;
  };
  let code = apiError?.result?.detail || apiError?.result?.title;

  if (!code && apiError?.response) {
    try {
      const parsed = JSON.parse(apiError.response) as {
        detail?: string;
        title?: string;
      };
      code = parsed.detail || parsed.title;
    } catch {
      code = undefined;
    }
  }

  if (code && PACKAGE_ERROR_KEY_MAP[code]) {
    return PACKAGE_ERROR_KEY_MAP[code];
  }
  return PACKAGE_FALLBACK_ERROR_KEY;
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
        onClick: (row: PackageListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-ban',
        tooltip: translate.instant('pages.package_management.deactivate_package'),
        color: 'danger',
        visible: () => defs.getIsActiveFilter() !== false,
        onClick: (row: PackageListItem) => defs.onDeactivate(row),
      },
      {
        icon: 'pi pi-check-circle',
        tooltip: translate.instant('pages.package_management.activate_package'),
        color: 'success',
        visible: () => defs.getIsActiveFilter() !== true,
        onClick: (row: PackageListItem) => defs.onActivate(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.package_management.delete_package'),
        color: 'danger',
        onClick: (row: PackageListItem) => defs.onDelete(row),
      },
    ],
  };
}