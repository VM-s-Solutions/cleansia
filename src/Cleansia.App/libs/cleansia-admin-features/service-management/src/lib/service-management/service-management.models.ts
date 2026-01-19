import { ServiceListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getServiceTableDefinition(
  defs: {
    onEdit: (row: ServiceListItem) => void;
    onDelete: (row: ServiceListItem) => void;
  },
  translate: TranslateService,
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
        onClick: (row: ServiceListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.service_management.delete_service'),
        color: 'danger',
        onClick: (row: ServiceListItem) => defs.onDelete(row),
      },
    ],
  };
}
