import { ServiceListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getServiceTableDefinition(
  defs: {
    onViewDetails: (row: ServiceListItem) => void;
    onEdit: (row: ServiceListItem) => void;
    onDelete: (row: ServiceListItem) => void;
  },
  translate: TranslateService,
  formatCurrency: (value: number | undefined) => string
): TableDefinition<ServiceListItem> {
  return {
    columns: [
      {
        id: 'name',
        headerName: translate.instant('pages.service_management.columns.name'),
        value: 'name',
        sortable: true,
        sortField: 'Name',
        columnClass: 'width-20',
      },
      {
        id: 'description',
        headerName: translate.instant(
          'pages.service_management.columns.description'
        ),
        value: (row?: ServiceListItem) => {
          if (!row?.description) return '';
          return row.description.length > 100
            ? row.description.substring(0, 100) + '...'
            : row.description;
        },
        columnClass: 'width-30',
      },
      {
        id: 'basePrice',
        headerName: translate.instant(
          'pages.service_management.columns.base_price'
        ),
        value: (row?: ServiceListItem) => formatCurrency(row?.basePrice),
        sortable: true,
        sortField: 'BasePrice',
        columnClass: 'width-15',
      },
      {
        id: 'perRoomPrice',
        headerName: translate.instant(
          'pages.service_management.columns.per_room_price'
        ),
        value: (row?: ServiceListItem) => formatCurrency(row?.perRoomPrice),
        sortable: true,
        sortField: 'PerRoomPrice',
        columnClass: 'width-15',
      },
      {
        id: 'actions',
        headerName: translate.instant(
          'pages.service_management.columns.actions'
        ),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: ServiceListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.service_management.edit_service'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: ServiceListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            tooltip: {
              title: translate.instant(
                'pages.service_management.delete_service'
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
