import { ReceiptTemplateListItem } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getReceiptTemplateTableDefinition(
  defs: {
    onEdit: (row: ReceiptTemplateListItem) => void;
    onActivate: (row: ReceiptTemplateListItem) => void;
    onDeactivate: (row: ReceiptTemplateListItem) => void;
    onDelete: (row: ReceiptTemplateListItem) => void;
  },
  translate: TranslateService
): TableDefinition<ReceiptTemplateListItem> {
  return {
    columns: [
      {
        id: 'templateName',
        headerName: translate.instant(
          'pages.template_management.columns.template_name'
        ),
        value: 'templateName',
        columnClass: 'width-20',
      },
      {
        id: 'countryName',
        headerName: translate.instant(
          'pages.template_management.columns.country'
        ),
        value: 'countryName',
        columnClass: 'width-15',
      },
      {
        id: 'languageCode',
        headerName: translate.instant(
          'pages.template_management.columns.language'
        ),
        value: 'languageCode',
        columnClass: 'width-10',
      },
      {
        id: 'version',
        headerName: translate.instant(
          'pages.template_management.columns.version'
        ),
        value: 'version',
        columnClass: 'width-10',
      },
      {
        id: 'isActive',
        headerName: translate.instant(
          'pages.template_management.columns.status'
        ),
        value: (row?: ReceiptTemplateListItem) =>
          row?.isActive
            ? translate.instant('global.status.active')
            : translate.instant('global.status.inactive'),
        columnClass: 'width-10',
      },
      {
        id: 'actions',
        headerName: translate.instant(
          'pages.template_management.columns.actions'
        ),
        columnActions: [
          {
            icon: 'pi pi-pencil',
            onClick: (row: ReceiptTemplateListItem) => defs.onEdit(row),
            buttonPalette: 'p-button-warning p-button-sm',
            tooltip: {
              title: translate.instant(
                'pages.template_management.edit_template'
              ),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-check',
            onClick: (row: ReceiptTemplateListItem) => defs.onActivate(row),
            buttonPalette: 'p-button-success p-button-sm',
            visible: (row: ReceiptTemplateListItem) => !row.isActive,
            tooltip: {
              title: translate.instant(
                'pages.template_management.activate_template'
              ),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-times',
            onClick: (row: ReceiptTemplateListItem) => defs.onDeactivate(row),
            buttonPalette: 'p-button-secondary p-button-sm',
            visible: (row: ReceiptTemplateListItem) => row.isActive,
            tooltip: {
              title: translate.instant(
                'pages.template_management.deactivate_template'
              ),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-trash',
            onClick: (row: ReceiptTemplateListItem) => defs.onDelete(row),
            buttonPalette: 'p-button-danger p-button-sm',
            visible: (row: ReceiptTemplateListItem) => !row.isActive,
            tooltip: {
              title: translate.instant(
                'pages.template_management.delete_template'
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
