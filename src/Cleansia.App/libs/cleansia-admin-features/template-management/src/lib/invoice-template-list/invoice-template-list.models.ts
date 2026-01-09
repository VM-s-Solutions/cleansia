import { InvoiceTemplateListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getInvoiceTemplateTableDefinition(
  defs: {
    onEdit: (row: InvoiceTemplateListItem) => void;
    onActivate: (row: InvoiceTemplateListItem) => void;
    onDeactivate: (row: InvoiceTemplateListItem) => void;
    onDelete: (row: InvoiceTemplateListItem) => void;
  },
  translate: TranslateService
): TableDefinition<InvoiceTemplateListItem> {
  return {
    columns: [
      {
        id: 'templateName',
        headerName: translate.instant('pages.template_management.columns.template_name'),
        value: 'templateName',
        columnClass: 'width-20',
      },
      {
        id: 'countryName',
        headerName: translate.instant('pages.template_management.columns.country'),
        value: 'countryName',
        columnClass: 'width-15',
      },
      {
        id: 'languageCode',
        headerName: translate.instant('pages.template_management.columns.language'),
        value: 'languageCode',
        columnClass: 'width-10',
      },
      {
        id: 'version',
        headerName: translate.instant('pages.template_management.columns.version'),
        value: 'version',
        columnClass: 'width-10',
      },
      {
        id: 'isActive',
        headerName: translate.instant('pages.template_management.columns.status'),
        value: (row?: InvoiceTemplateListItem) =>
          row?.isActive
            ? translate.instant('global.status.active')
            : translate.instant('global.status.inactive'),
        columnClass: 'width-10',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.template_management.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: InvoiceTemplateListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant('pages.template_management.edit_template'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-check',
            onClick: (row: InvoiceTemplateListItem) => defs.onActivate(row),
            buttonPalette: 'p-button-success p-button-sm',
            visible: (row: InvoiceTemplateListItem) => row.isActive !== true,
            tooltip: {
              title: translate.instant('pages.template_management.activate_template'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-times',
            onClick: (row: InvoiceTemplateListItem) => defs.onDeactivate(row),
            buttonPalette: 'p-button-secondary p-button-sm',
            visible: (row: InvoiceTemplateListItem) => row.isActive === true,
            tooltip: {
              title: translate.instant('pages.template_management.deactivate_template'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: InvoiceTemplateListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            visible: (row: InvoiceTemplateListItem) => row.isActive !== true,
            tooltip: {
              title: translate.instant('pages.template_management.delete_template'),
              position: 'above',
            },
          },
        ],
        columnClass: 'width-15',
      },
    ],
  };
}
