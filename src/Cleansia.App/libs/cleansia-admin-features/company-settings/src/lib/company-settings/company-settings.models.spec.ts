import { SetTenantSettingCommand, TenantSettingDto, TenantSettingValueType } from '@cleansia/admin-services';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import {
  buildSetTenantSettingCommand,
  formatBoolSetting,
  formatSettingRange,
  formatSettingValue,
  getCompanySettingsTableDefinition,
  getSettingCategoryKey,
  getSettingDescriptionKey,
  parseBoolSetting,
} from './company-settings.models';

const translate = { instant: (key: string) => key } as unknown as TranslateService;

const intSetting = TenantSettingDto.fromJS({
  key: 'retention.stale_devices.days',
  category: 'retention',
  valueType: TenantSettingValueType.Int,
  min: 1,
  max: 36500,
  defaultValue: '90',
  effectiveValue: '120',
  isOverridden: true,
});

const boolSetting = TenantSettingDto.fromJS({
  key: 'retention.expired_codes.enabled',
  category: 'retention',
  valueType: TenantSettingValueType.Bool,
  defaultValue: 'true',
  effectiveValue: 'false',
  isOverridden: true,
});

describe('company-settings models', () => {
  describe('parseBoolSetting / formatBoolSetting', () => {
    it('reads the catalogue canonical "true" as checked and anything else as unchecked', () => {
      expect(parseBoolSetting('true')).toBe(true);
      expect(parseBoolSetting(' True ')).toBe(true);
      expect(parseBoolSetting('false')).toBe(false);
      expect(parseBoolSetting('')).toBe(false);
      expect(parseBoolSetting(undefined)).toBe(false);
    });

    it('writes the canonical lower-case form the catalogue stores', () => {
      expect(formatBoolSetting(true)).toBe('true');
      expect(formatBoolSetting(false)).toBe('false');
    });
  });

  describe('translation keys', () => {
    it('nests the dotted catalogue key under the descriptions block', () => {
      expect(getSettingDescriptionKey('retention.stale_devices.days')).toBe(
        'pages.company_settings.descriptions.retention.stale_devices.days'
      );
    });

    it('nests the category under the categories block', () => {
      expect(getSettingCategoryKey('retention')).toBe('pages.company_settings.categories.retention');
    });
  });

  describe('formatSettingValue', () => {
    it('renders a bool as the translated yes / no', () => {
      expect(formatSettingValue(boolSetting, 'true', translate)).toBe('global.yes');
      expect(formatSettingValue(boolSetting, 'false', translate)).toBe('global.no');
    });

    it('renders an int verbatim and a missing value blank', () => {
      expect(formatSettingValue(intSetting, '120', translate)).toBe('120');
      expect(formatSettingValue(intSetting, undefined, translate)).toBe('');
    });
  });

  describe('formatSettingRange', () => {
    it('renders the floor and ceiling of an int setting', () => {
      expect(formatSettingRange(intSetting)).toBe('1 – 36500');
    });

    it('is blank for a setting without a range', () => {
      expect(formatSettingRange(boolSetting)).toBe('');
    });
  });

  describe('buildSetTenantSettingCommand', () => {
    it('builds the generated command with the whole wire body', () => {
      const command = buildSetTenantSettingCommand('retention.stale_devices.days', '120');

      expect(command).toBeInstanceOf(SetTenantSettingCommand);
      expect(command.toJSON()).toEqual({ key: 'retention.stale_devices.days', value: '120' });
    });
  });

  describe('getCompanySettingsTableDefinition', () => {
    const grantAll = { hasPolicy: () => true } as unknown as PermissionService;
    const defs = {
      onEdit: jest.fn(),
      onSave: jest.fn(),
      onCancel: jest.fn(),
      onReset: jest.fn(),
      isEditing: jest.fn().mockReturnValue(false),
      isBusy: jest.fn().mockReturnValue(false),
    };

    const definition = () => getCompanySettingsTableDefinition(defs, translate, grantAll);
    const action = (icon: string) => {
      const found = definition().actions.find((a) => a.icon === icon);
      if (!found) throw new Error(`no action ${icon}`);
      return found;
    };

    beforeEach(() => {
      defs.isEditing.mockReturnValue(false);
      defs.isBusy.mockReturnValue(false);
    });

    it('renders the key raw, the description, category, range, default and source through the columns', () => {
      const { columns } = definition();
      const value = (id: string, row: TenantSettingDto) => {
        const column = columns.find((c) => c.id === id);
        if (!column?.getValue) throw new Error(`no getValue on ${id}`);
        return column.getValue(row);
      };

      expect(value('key', intSetting)).toBe('retention.stale_devices.days');
      expect(value('description', intSetting)).toBe(
        'pages.company_settings.descriptions.retention.stale_devices.days'
      );
      expect(value('category', intSetting)).toBe('pages.company_settings.categories.retention');
      expect(value('range', intSetting)).toBe('1 – 36500');
      expect(value('default', boolSetting)).toBe('global.yes');
      expect(value('source', intSetting)).toBe('pages.company_settings.source.overridden');
      expect(value('source', TenantSettingDto.fromJS({ ...intSetting, isOverridden: false }))).toBe(
        'pages.company_settings.source.default'
      );
    });

    it('shows edit and reset on a row at rest, save and cancel on the row being edited', () => {
      expect(action('pi pi-pencil').visible?.(intSetting)).toBe(true);
      expect(action('pi pi-replay').visible?.(intSetting)).toBe(true);
      expect(action('pi pi-check').visible?.(intSetting)).toBe(false);
      expect(action('pi pi-times').visible?.(intSetting)).toBe(false);

      defs.isEditing.mockReturnValue(true);

      expect(action('pi pi-pencil').visible?.(intSetting)).toBe(false);
      expect(action('pi pi-replay').visible?.(intSetting)).toBe(false);
      expect(action('pi pi-check').visible?.(intSetting)).toBe(true);
      expect(action('pi pi-times').visible?.(intSetting)).toBe(true);
    });

    it('offers reset only on a row that holds an override', () => {
      const atDefault = TenantSettingDto.fromJS({ ...intSetting, isOverridden: false });

      expect(action('pi pi-replay').visible?.(atDefault)).toBe(false);
    });

    it('gates edit on the update policy and reset on the delete policy the endpoints use', () => {
      const denyUpdate = {
        hasPolicy: (policy: string) => policy !== Policy.CanUpdateTenantConfiguration,
      } as unknown as PermissionService;
      const denyDelete = {
        hasPolicy: (policy: string) => policy !== Policy.CanDeleteTenantConfiguration,
      } as unknown as PermissionService;

      const withoutUpdate = getCompanySettingsTableDefinition(defs, translate, denyUpdate).actions;
      const withoutDelete = getCompanySettingsTableDefinition(defs, translate, denyDelete).actions;

      expect(withoutUpdate.find((a) => a.icon === 'pi pi-pencil')?.visible?.(intSetting)).toBe(false);
      expect(withoutUpdate.find((a) => a.icon === 'pi pi-replay')?.visible?.(intSetting)).toBe(true);
      expect(withoutDelete.find((a) => a.icon === 'pi pi-pencil')?.visible?.(intSetting)).toBe(true);
      expect(withoutDelete.find((a) => a.icon === 'pi pi-replay')?.visible?.(intSetting)).toBe(false);
    });

    it('disables every action on a row with a save or reset in flight', () => {
      defs.isBusy.mockReturnValue(true);

      for (const icon of ['pi pi-pencil', 'pi pi-replay', 'pi pi-check', 'pi pi-times']) {
        expect(action(icon).disabled?.(intSetting)).toBe(true);
      }
    });

    it('routes each action to its handler with the row', () => {
      action('pi pi-pencil').onClick(intSetting);
      action('pi pi-check').onClick(intSetting);
      action('pi pi-times').onClick(intSetting);
      action('pi pi-replay').onClick(intSetting);

      expect(defs.onEdit).toHaveBeenCalledWith(intSetting);
      expect(defs.onSave).toHaveBeenCalledWith(intSetting);
      expect(defs.onCancel).toHaveBeenCalledWith(intSetting);
      expect(defs.onReset).toHaveBeenCalledWith(intSetting);
    });
  });
});
