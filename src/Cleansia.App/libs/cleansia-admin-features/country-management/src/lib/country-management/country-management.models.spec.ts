import { CountryListItem } from '@cleansia/admin-services';
import { PermissionService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import {
  getCountryTableDefinition,
} from './country-management.models';

describe('country-management models', () => {
  const translate = { instant: (k: string) => k } as TranslateService;
  const permissions = { hasPolicy: () => true } as unknown as PermissionService;
  const defaultRow = CountryListItem.fromJS({ id: 'c-1', isDefaultMarket: true });
  const otherRow = CountryListItem.fromJS({ id: 'c-2', isDefaultMarket: false });

  function tableDefinition() {
    return getCountryTableDefinition(
      { onEdit: jest.fn(), onDelete: jest.fn(), onSetDefaultMarket: jest.fn() },
      translate,
      permissions,
      undefined
    );
  }

  describe('default-market column', () => {
    it('says yes on the default market and draws a dash, never a blank, on every other row', () => {
      const column = tableDefinition().columns.find((c) => c.id === 'isDefaultMarket');

      expect(column).toBeDefined();
      expect(column?.field).toBe('isDefaultMarket');
      expect(column?.header).toBe('pages.country_management.columns.default_market');
      expect(column?.getValue?.(defaultRow)).toBe('global.yes');
      expect(column?.getValue?.(otherRow)).toBe('—');
    });
  });

  describe('set-default-market action', () => {
    function starAction() {
      const action = tableDefinition().actions.find((a) => a.icon === 'pi pi-star');
      if (!action) throw new Error('set-default-market action missing');
      return action;
    }

    it('is offered on a row that is not the default market', () => {
      expect(starAction().visible?.(otherRow)).toBe(true);
    });

    it('is hidden on the row that already is the default market', () => {
      expect(starAction().visible?.(defaultRow)).toBe(false);
    });

    it('carries the translated tooltip and delegates the click', () => {
      const onSetDefaultMarket = jest.fn();
      const action = getCountryTableDefinition(
        { onEdit: jest.fn(), onDelete: jest.fn(), onSetDefaultMarket },
        translate,
        permissions
      ).actions.find((a) => a.icon === 'pi pi-star');

      expect(action?.tooltip).toBe('pages.country_management.set_default_market');
      action?.onClick(otherRow);
      expect(onSetDefaultMarket).toHaveBeenCalledWith(otherRow);
    });
  });
});
