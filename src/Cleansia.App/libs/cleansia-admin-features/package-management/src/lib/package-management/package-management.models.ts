import { PackageListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getPackageTableDefinition(
  defs: {
    onEdit: (row: PackageListItem) => void;
    onDelete: (row: PackageListItem) => void;
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
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.package_management.delete_package'),
        color: 'danger',
        onClick: (row: PackageListItem) => defs.onDelete(row),
      },
    ],
  };
}