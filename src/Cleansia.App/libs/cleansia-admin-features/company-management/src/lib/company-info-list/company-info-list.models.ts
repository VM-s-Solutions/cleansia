import { TemplateRef } from '@angular/core';
import { CompanyInfoListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getCompanyInfoTableDefinition(
  defs: {
    onEdit: (row: CompanyInfoListItem) => void;
    onDelete: (row: CompanyInfoListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<CompanyInfoListItem>
): { columns: TableColumn<CompanyInfoListItem>[]; actions: TableAction<CompanyInfoListItem>[] } {
  return {
    columns: [
      {
        id: 'legalName',
        field: 'legalName',
        header: translate.instant('pages.company_management.columns.legal_name'),
        sortable: true,
        width: '20%',
      },
      {
        id: 'tradingName',
        field: 'tradingName',
        header: translate.instant('pages.company_management.columns.trading_name'),
        sortable: true,
        width: '15%',
      },
      {
        id: 'countryName',
        field: 'countryName',
        header: translate.instant('pages.company_management.columns.country'),
        sortable: true,
        width: '15%',
      },
      {
        id: 'city',
        field: 'city',
        header: translate.instant('pages.company_management.columns.city'),
        sortable: true,
        width: '15%',
      },
      {
        id: 'email',
        field: 'email',
        header: translate.instant('pages.company_management.columns.email'),
        width: '15%',
      },
      {
        id: 'isActive',
        field: 'isActive',
        header: translate.instant('pages.company_management.columns.status'),
        customTemplate: statusTemplate,
        width: '10%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.company_management.edit_company'),
        color: 'warning',
        onClick: (row: CompanyInfoListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.company_management.delete_company'),
        color: 'danger',
        onClick: (row: CompanyInfoListItem) => defs.onDelete(row),
      },
    ],
  };
}
