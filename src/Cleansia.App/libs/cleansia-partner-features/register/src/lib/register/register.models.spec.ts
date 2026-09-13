import { MarketListItem, Translation } from '@cleansia/partner-services';
import { defaultMarket, marketOptionLabel } from './register.models';

function translation(name: string): Translation {
  return new Translation({ name, description: undefined, tagline: undefined });
}

function market(overrides: Partial<MarketListItem>): MarketListItem {
  return new MarketListItem({
    countryId: 'cz-id',
    isoCode: 'CZE',
    isoAlpha2: 'CZ',
    name: 'Czech Republic',
    currencyCode: 'CZK',
    isDefault: false,
    translations: { cs: translation('Česko') },
    ...overrides,
  } as MarketListItem);
}

describe('marketOptionLabel', () => {
  it('prints the name in the active language beside the currency code', () => {
    expect(marketOptionLabel(market({}), 'cs')).toBe('Česko · CZK');
  });

  it('falls back to the wire name when the language has no translation', () => {
    expect(marketOptionLabel(market({}), 'uk')).toBe('Czech Republic · CZK');
  });

  it('falls back to the ISO code when the wire carries no name at all', () => {
    expect(
      marketOptionLabel(market({ name: undefined, translations: undefined, currencyCode: undefined }), 'en')
    ).toBe('CZE · ');
  });
});

describe('defaultMarket', () => {
  it('picks the market flagged as default', () => {
    const svk = market({ countryId: 'svk-id', isDefault: true });

    expect(defaultMarket([market({}), svk])).toBe(svk);
  });

  it('picks the first when none is flagged', () => {
    const cz = market({});

    expect(defaultMarket([cz, market({ countryId: 'svk-id' })])).toBe(cz);
  });

  it('picks nothing from an empty directory', () => {
    expect(defaultMarket([])).toBeNull();
  });
});
