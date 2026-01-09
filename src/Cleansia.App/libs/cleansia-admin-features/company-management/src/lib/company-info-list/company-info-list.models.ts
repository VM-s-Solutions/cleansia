import { CompanyInfoListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getCompanyInfoTableDefinition(
  defs: {
    onEdit: (row: CompanyInfoListItem) => void;
    onDelete: (row: CompanyInfoListItem) => void;
  },
  translate: TranslateService
): TableDefinition<CompanyInfoListItem> {
  return {
    columns: [
      {
        id: 'legalName',
        headerName: translate.instant(
          'pages.company_management.columns.legal_name'
        ),
        value: 'legalName',
        sortable: true,
        sortField: 'LegalName',
        columnClass: 'width-20',
      },
      {
        id: 'tradingName',
        headerName: translate.instant(
          'pages.company_management.columns.trading_name'
        ),
        value: 'tradingName',
        sortable: true,
        sortField: 'TradingName',
        columnClass: 'width-15',
      },
      {
        id: 'countryName',
        headerName: translate.instant(
          'pages.company_management.columns.country'
        ),
        value: 'countryName',
        sortable: true,
        sortField: 'CountryId',
        columnClass: 'width-15',
      },
      {
        id: 'city',
        headerName: translate.instant('pages.company_management.columns.city'),
        value: 'city',
        sortable: true,
        sortField: 'City',
        columnClass: 'width-15',
      },
      {
        id: 'email',
        headerName: translate.instant('pages.company_management.columns.email'),
        value: 'email',
        columnClass: 'width-15',
      },
      {
        id: 'isActive',
        headerName: translate.instant(
          'pages.company_management.columns.status'
        ),
        value: (row?: CompanyInfoListItem) =>
          row?.isActive
            ? translate.instant('global.status.active')
            : translate.instant('global.status.inactive'),
        columnClass: 'width-10',
      },
      {
        id: 'actions',
        headerName: translate.instant(
          'pages.company_management.columns.actions'
        ),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: CompanyInfoListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.company_management.edit_company'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: CompanyInfoListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            tooltip: {
              title: translate.instant(
                'pages.company_management.delete_company'
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
