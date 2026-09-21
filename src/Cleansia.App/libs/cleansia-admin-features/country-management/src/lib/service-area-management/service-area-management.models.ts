import { TemplateRef } from '@angular/core';
import { CountryListItem, ServiceCityDto } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export interface ServiceCityForm {
  name: string;
  zipPrefix: string;
  isActive: boolean;
}

export const EMPTY_SERVICE_CITY_FORM: ServiceCityForm = { name: '', zipPrefix: '', isActive: true };

export function getServicedCountryTableColumns(
  translate: TranslateService,
  servicedTemplate?: TemplateRef<CountryListItem>
): TableColumn<CountryListItem>[] {
  return [
    {
      id: 'name',
      field: 'name',
      header: translate.instant('pages.service_area_management.columns.country'),
      width: '55%',
    },
    {
      id: 'isoCode',
      field: 'isoCode',
      header: translate.instant('pages.service_area_management.columns.iso_code'),
      width: '25%',
    },
    {
      id: 'isServiced',
      field: 'id',
      header: translate.instant('pages.service_area_management.columns.is_serviced'),
      align: 'center',
      width: '20%',
      customTemplate: servicedTemplate,
    },
  ];
}

export function getServiceCityTableDefinition(
  defs: {
    canManage: boolean;
    onEdit: (row: ServiceCityDto) => void;
    onDelete: (row: ServiceCityDto) => void;
  },
  translate: TranslateService,
  activeTemplate?: TemplateRef<ServiceCityDto>
): { columns: TableColumn<ServiceCityDto>[]; actions: TableAction<ServiceCityDto>[] } {
  return {
    columns: [
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.service_area_management.columns.city'),
        width: '45%',
      },
      {
        id: 'zipPrefix',
        field: 'zipPrefix',
        header: translate.instant('pages.service_area_management.columns.zip_prefix'),
        getValue: (row) => row.zipPrefix || '—',
        width: '20%',
      },
      {
        id: 'isActive',
        field: 'isActive',
        header: translate.instant('pages.service_area_management.columns.is_active'),
        align: 'center',
        width: '20%',
        customTemplate: activeTemplate,
      },
    ],
    actions: defs.canManage
      ? [
          {
            icon: 'pi pi-pencil',
            tooltip: translate.instant('pages.service_area_management.cities.edit'),
            onClick: (row) => defs.onEdit(row),
          },
          {
            icon: 'pi pi-trash',
            color: 'danger',
            tooltip: translate.instant('pages.service_area_management.cities.delete'),
            onClick: (row) => defs.onDelete(row),
          },
        ]
      : [],
  };
}
