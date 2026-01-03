import { ServiceListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getServiceTableDefinition(
  defs: {
    onViewDetails: (row: ServiceListItem) => void;
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
            icon: 'pi pi-eye',
            onClick: (row: ServiceListItem) => defs.onViewDetails(row),
            buttonPalette: 'p-button-info p-button-sm',
            tooltip: {
              title: translate.instant(
                'pages.service_management.view_details'
              ),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-10',
      },
    ],
  };
}