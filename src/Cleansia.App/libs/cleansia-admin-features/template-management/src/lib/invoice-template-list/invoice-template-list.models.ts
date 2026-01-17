import { TemplateRef } from '@angular/core';
import { InvoiceTemplateListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getInvoiceTemplateTableDefinition(
  defs: {
    onEdit: (row: InvoiceTemplateListItem) => void;
    onActivate: (row: InvoiceTemplateListItem) => void;
    onDeactivate: (row: InvoiceTemplateListItem) => void;
    onDelete: (row: InvoiceTemplateListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<InvoiceTemplateListItem>
): { columns: TableColumn<InvoiceTemplateListItem>[]; actions: TableAction<InvoiceTemplateListItem>[] } {
  return {
    columns: [
      {
        id: 'templateName',
        field: 'templateName',
        header: translate.instant('pages.template_management.columns.template_name'),
        width: '20%',
      },
      {
        id: 'countryName',
        field: 'countryName',
        header: translate.instant('pages.template_management.columns.country'),
        width: '15%',
      },
      {
        id: 'languageCode',
        field: 'languageCode',
        header: translate.instant('pages.template_management.columns.language'),
        width: '10%',
      },
      {
        id: 'version',
        field: 'version',
        header: translate.instant('pages.template_management.columns.version'),
        width: '10%',
      },
      {
        id: 'isActive',
        field: 'isActive',
        header: translate.instant('pages.template_management.columns.status'),
        customTemplate: statusTemplate,
        width: '10%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.template_management.edit_template'),
        color: 'warning',
        onClick: (row: InvoiceTemplateListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-check',
        tooltip: translate.instant('pages.template_management.activate_template'),
        color: 'success',
        onClick: (row: InvoiceTemplateListItem) => defs.onActivate(row),
        visible: (row: InvoiceTemplateListItem) => !row.isActive,
      },
      {
        icon: 'pi pi-times',
        tooltip: translate.instant('pages.template_management.deactivate_template'),
        onClick: (row: InvoiceTemplateListItem) => defs.onDeactivate(row),
        visible: (row: InvoiceTemplateListItem) => row.isActive,
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.template_management.delete_template'),
        color: 'danger',
        onClick: (row: InvoiceTemplateListItem) => defs.onDelete(row),
        visible: (row: InvoiceTemplateListItem) => !row.isActive,
      },
    ],
  };
}
