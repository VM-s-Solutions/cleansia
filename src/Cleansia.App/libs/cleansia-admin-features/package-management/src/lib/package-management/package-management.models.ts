import { PackageListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getPackageTableDefinition(
  defs: {
    onViewDetails: (row: PackageListItem) => void;
    onEdit: (row: PackageListItem) => void;
    onDelete: (row: PackageListItem) => void;
  },
  translate: TranslateService,
  formatCurrency: (value: number | undefined) => string
): TableDefinition<PackageListItem> {
  return {
    columns: [
      {
        id: 'name',
        headerName: translate.instant('pages.package_management.columns.name'),
        value: 'name',
        sortable: true,
        sortField: 'Name',
        columnClass: 'width-25',
      },
      {
        id: 'description',
        headerName: translate.instant(
          'pages.package_management.columns.description'
        ),
        value: (row?: PackageListItem) => {
          if (!row?.description) return '';
          return row.description.length > 100
            ? row.description.substring(0, 100) + '...'
            : row.description;
        },
        columnClass: 'width-35',
      },
      {
        id: 'price',
        headerName: translate.instant('pages.package_management.columns.price'),
        value: (row?: PackageListItem) => formatCurrency(row?.price),
        sortable: true,
        sortField: 'Price',
        columnClass: 'width-15',
      },
      {
        id: 'actions',
        headerName: translate.instant(
          'pages.package_management.columns.actions'
        ),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: PackageListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.package_management.edit_package'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: PackageListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            tooltip: {
              title: translate.instant(
                'pages.package_management.delete_package'
              ),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-15',
      },
    ],
  };
}