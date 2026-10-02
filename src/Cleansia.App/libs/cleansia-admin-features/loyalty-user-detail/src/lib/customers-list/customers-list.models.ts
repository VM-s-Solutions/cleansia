import { TemplateRef } from '@angular/core';
import { AdminCustomerListItem } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export type CustomerStatusFilter = 'all' | 'active' | 'inactive';

export const DEFAULT_CUSTOMER_STATUS: CustomerStatusFilter = 'active';

export function mapStatusFilterToIsActive(value: CustomerStatusFilter): boolean | undefined {
  if (value === 'active') return true;
  if (value === 'inactive') return false;
  return undefined;
}

export function getCustomerTableDefinition(
  defs: {
    onView: (row: AdminCustomerListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<AdminCustomerListItem>
): { columns: TableColumn<AdminCustomerListItem>[]; actions: TableAction<AdminCustomerListItem>[] } {
  return {
    columns: [
      {
        id: 'name',
        field: 'lastName',
        header: translate.instant('pages.customers.columns.name'),
        getValue: (row: AdminCustomerListItem) =>
          `${row.firstName ?? ''} ${row.lastName ?? ''}`.trim(),
        sortable: true,
        width: '20%',
      },
      {
        id: 'email',
        field: 'email',
        header: translate.instant('pages.customers.columns.email'),
        sortable: true,
        width: '22%',
      },
      {
        id: 'phone',
        field: 'phoneNumber',
        header: translate.instant('pages.customers.columns.phone'),
        sortable: true,
        width: '14%',
      },
      {
        id: 'status',
        field: 'isActive',
        header: translate.instant('pages.customers.columns.status'),
        align: 'center',
        customTemplate: statusTemplate,
        width: '12%',
      },
      {
        id: 'emailConfirmed',
        field: 'isEmailConfirmed',
        header: translate.instant('pages.customers.columns.email_confirmed'),
        align: 'center',
        getValue: (row: AdminCustomerListItem) =>
          translate.instant(row.isEmailConfirmed ? 'global.yes' : 'global.no'),
        width: '12%',
      },
      {
        id: 'createdOn',
        numeric: true,
        field: 'createdOn',
        header: translate.instant('pages.customers.columns.created_on'),
        getValue: (row: AdminCustomerListItem) => formatDate(row.createdOn, translate.currentLang),
        sortable: true,
        width: '14%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.loyalty_user_detail.view_link'),
        color: 'info',
        onClick: (row: AdminCustomerListItem) => defs.onView(row),
      },
    ],
  };
}
