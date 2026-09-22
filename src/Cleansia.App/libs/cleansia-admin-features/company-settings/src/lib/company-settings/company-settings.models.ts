import { TemplateRef } from '@angular/core';
import { SetTenantSettingCommand, TenantSettingDto, TenantSettingValueType } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

const BOOL_TRUE = 'true';
const BOOL_FALSE = 'false';

export function parseBoolSetting(value: string | undefined): boolean {
  return value?.trim().toLowerCase() === BOOL_TRUE;
}

export function formatBoolSetting(checked: boolean): string {
  return checked ? BOOL_TRUE : BOOL_FALSE;
}

// A catalogue key is dotted (`retention.stale_devices.days`), which ngx-translate reads as a path,
// so the locale nests each key's copy under the block rather than flattening it.
export function getSettingDescriptionKey(key: string | undefined): string {
  return `pages.company_settings.descriptions.${key ?? ''}`;
}

export function getSettingCategoryKey(category: string | undefined): string {
  return `pages.company_settings.categories.${category ?? ''}`;
}

export function formatSettingValue(
  setting: TenantSettingDto,
  value: string | undefined,
  translate: TranslateService
): string {
  if (setting.valueType === TenantSettingValueType.Bool) {
    return translate.instant(parseBoolSetting(value) ? 'global.yes' : 'global.no');
  }
  if (setting.valueType === TenantSettingValueType.Email && !value) {
    return translate.instant('pages.company_settings.every_administrator');
  }
  return value ?? '';
}

export function formatSettingRange(setting: TenantSettingDto): string {
  if (setting.min === undefined && setting.max === undefined) return '';
  return `${setting.min ?? ''} – ${setting.max ?? ''}`;
}

export function buildSetTenantSettingCommand(key: string, value: string): SetTenantSettingCommand {
  const command = new SetTenantSettingCommand();
  command.key = key;
  command.value = value;
  return command;
}

export function getCompanySettingsTableDefinition(
  defs: {
    onEdit: (row: TenantSettingDto) => void;
    onSave: (row: TenantSettingDto) => void;
    onCancel: (row: TenantSettingDto) => void;
    onReset: (row: TenantSettingDto) => void;
    isEditing: (row: TenantSettingDto) => boolean;
    isBusy: (row: TenantSettingDto) => boolean;
  },
  translate: TranslateService,
  permissions: PermissionService,
  valueTemplate?: TemplateRef<TenantSettingDto>
): { columns: TableColumn<TenantSettingDto>[]; actions: TableAction<TenantSettingDto>[] } {
  return {
    columns: [
      {
        id: 'key',
        field: 'key',
        header: translate.instant('pages.company_settings.columns.key'),
        getValue: (row: TenantSettingDto) => row.key ?? '',
        width: '18%',
      },
      {
        id: 'description',
        field: 'description',
        header: translate.instant('pages.company_settings.columns.description'),
        getValue: (row: TenantSettingDto) => translate.instant(getSettingDescriptionKey(row.key)),
        width: '24%',
      },
      {
        id: 'category',
        field: 'category',
        header: translate.instant('pages.company_settings.columns.category'),
        getValue: (row: TenantSettingDto) => translate.instant(getSettingCategoryKey(row.category)),
        width: '12%',
      },
      {
        id: 'range',
        numeric: true,
        field: 'min',
        header: translate.instant('pages.company_settings.columns.range'),
        getValue: (row: TenantSettingDto) => formatSettingRange(row),
        width: '8%',
      },
      {
        id: 'default',
        field: 'defaultValue',
        header: translate.instant('pages.company_settings.columns.default'),
        getValue: (row: TenantSettingDto) => formatSettingValue(row, row.defaultValue, translate),
        width: '8%',
      },
      {
        id: 'value',
        field: 'effectiveValue',
        header: translate.instant('pages.company_settings.columns.value'),
        getValue: (row: TenantSettingDto) => formatSettingValue(row, row.effectiveValue, translate),
        customTemplate: valueTemplate,
        width: '11%',
      },
      {
        id: 'source',
        field: 'isOverridden',
        header: translate.instant('pages.company_settings.columns.source'),
        getValue: (row: TenantSettingDto) =>
          translate.instant(
            row.isOverridden ? 'pages.company_settings.source.overridden' : 'pages.company_settings.source.default'
          ),
        width: '9%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.company_settings.edit'),
        color: 'warning',
        visible: (row: TenantSettingDto) =>
          !defs.isEditing(row) && permissions.hasPolicy(Policy.CanUpdateTenantConfiguration),
        disabled: (row: TenantSettingDto) => defs.isBusy(row),
        onClick: (row: TenantSettingDto) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-check',
        tooltip: translate.instant('pages.company_settings.save'),
        color: 'success',
        visible: (row: TenantSettingDto) => defs.isEditing(row),
        disabled: (row: TenantSettingDto) => defs.isBusy(row),
        onClick: (row: TenantSettingDto) => defs.onSave(row),
      },
      {
        icon: 'pi pi-times',
        tooltip: translate.instant('pages.company_settings.cancel'),
        color: 'info',
        visible: (row: TenantSettingDto) => defs.isEditing(row),
        disabled: (row: TenantSettingDto) => defs.isBusy(row),
        onClick: (row: TenantSettingDto) => defs.onCancel(row),
      },
      {
        icon: 'pi pi-replay',
        tooltip: translate.instant('pages.company_settings.reset'),
        color: 'danger',
        visible: (row: TenantSettingDto) =>
          row.isOverridden && !defs.isEditing(row) && permissions.hasPolicy(Policy.CanDeleteTenantConfiguration),
        disabled: (row: TenantSettingDto) => defs.isBusy(row),
        onClick: (row: TenantSettingDto) => defs.onReset(row),
      },
    ],
  };
}
