import { TemplateRef } from '@angular/core';
import { CountryListItem } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import {
  getCountryTableDefinition,
  resolveCountryErrorKey,
} from './country-management.models';

describe('country-management models', () => {
  const translate = { instant: (k: string) => k } as TranslateService;
  const defaultRow = CountryListItem.fromJS({ id: 'c-1', isDefaultMarket: true });
  const otherRow = CountryListItem.fromJS({ id: 'c-2', isDefaultMarket: false });

  function tableDefinition() {
    return getCountryTableDefinition(
      { onEdit: jest.fn(), onDelete: jest.fn(), onSetDefaultMarket: jest.fn() },
      translate,
      undefined,
      {} as TemplateRef<CountryListItem>
    );
  }

  describe('default-market badge column', () => {
    it('renders the badge through its template on the isDefaultMarket field', () => {
      const column = tableDefinition().columns.find((c) => c.id === 'isDefaultMarket');

      expect(column).toBeDefined();
      expect(column?.field).toBe('isDefaultMarket');
      expect(column?.header).toBe('pages.country_management.columns.default_market');
      expect(column?.customTemplate).toBeDefined();
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
        translate
      ).actions.find((a) => a.icon === 'pi pi-star');

      expect(action?.tooltip).toBe('pages.country_management.set_default_market');
      action?.onClick(otherRow);
      expect(onSetDefaultMarket).toHaveBeenCalledWith(otherRow);
    });
  });

  describe('resolveCountryErrorKey', () => {
    it.each([
      ['country.not_serviced', 'api.country.not_serviced'],
      ['country.market_not_ready', 'api.country.market_not_ready'],
      [
        'country.default_market_changed_concurrently',
        'api.country.default_market_changed_concurrently',
      ],
      ['country.not_found', 'api.country.not_found'],
    ])('maps %s from the parsed problem detail', (code, key) => {
      expect(resolveCountryErrorKey({ result: { detail: code } })).toBe(key);
    });

    it('reads the title when the detail is absent', () => {
      expect(
        resolveCountryErrorKey({ result: { title: 'country.not_serviced' } })
      ).toBe('api.country.not_serviced');
    });

    it('parses the raw response body when the client did not', () => {
      expect(
        resolveCountryErrorKey({
          response: JSON.stringify({ detail: 'country.market_not_ready' }),
        })
      ).toBe('api.country.market_not_ready');
    });

    it('falls back to the generic key on an unknown code or a body that is not JSON', () => {
      expect(resolveCountryErrorKey({ result: { detail: 'x.y' } })).toBe(
        'api.common.error_occurred'
      );
      expect(resolveCountryErrorKey({ response: 'not json' })).toBe(
        'api.common.error_occurred'
      );
      expect(resolveCountryErrorKey(undefined)).toBe('api.common.error_occurred');
    });
  });
});
