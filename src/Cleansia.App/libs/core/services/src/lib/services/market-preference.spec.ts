import {
  marketCountryOptions,
  PREFERRED_MARKET_KEY,
  persistPreferredMarket,
  readPreferredMarket,
  resolveMarket,
} from './market-preference';

// The default is deliberately NOT first, so "the default" and "the first listed" are told apart.
const MARKETS = [
  { isoCode: 'SVK', isDefault: false },
  { isoCode: 'CZE', isDefault: true },
];

describe('readPreferredMarket', () => {
  it('reads the market cookie out of a cookie header', () => {
    expect(readPreferredMarket('preferred_language=cs; preferred_market=SVK; theme=dark')).toBe('SVK');
  });

  it('answers null with no header and with no market cookie', () => {
    expect(readPreferredMarket(null)).toBeNull();
    expect(readPreferredMarket(undefined)).toBeNull();
    expect(readPreferredMarket('preferred_language=cs')).toBeNull();
  });

  // A cookie is attacker-controlled text. It is only ever compared against the market list, so
  // anything that is not three letters cannot match a listed market and is dropped here rather
  // than carried around.
  it('drops a value that is not a three-letter code', () => {
    expect(readPreferredMarket('preferred_market=<script>alert(1)</script>')).toBeNull();
    expect(readPreferredMarket('preferred_market=')).toBeNull();
    expect(readPreferredMarket('preferred_market=SVKX')).toBeNull();
  });

  it('upper-cases the code so the comparison is case-insensitive', () => {
    expect(readPreferredMarket('preferred_market=svk')).toBe('SVK');
  });
});

describe('resolveMarket', () => {
  it('takes the stored code when it names a listed market', () => {
    expect(resolveMarket(MARKETS, 'SVK')).toBe(MARKETS[0]);
  });

  it('falls to the default market when nothing is stored', () => {
    expect(resolveMarket(MARKETS, null)).toBe(MARKETS[1]);
  });

  it('falls to the default market when the stored code names a delisted market', () => {
    expect(resolveMarket(MARKETS, 'POL')).toBe(MARKETS[1]);
  });

  it('falls to the first listed market when none is flagged default', () => {
    const noDefault = [
      { isoCode: 'DEU', isDefault: false },
      { isoCode: 'SVK', isDefault: false },
    ];
    expect(resolveMarket(noDefault, null)).toBe(noDefault[0]);
  });

  it('answers null for an empty list', () => {
    expect(resolveMarket([], 'CZE')).toBeNull();
  });
});

describe('persistPreferredMarket', () => {
  afterEach(() => {
    document.cookie = `${PREFERRED_MARKET_KEY}=; path=/; max-age=0`;
  });

  it('writes the cookie, and nothing to localStorage', () => {
    localStorage.clear();

    persistPreferredMarket('SVK');

    expect(document.cookie).toContain('preferred_market=SVK');
    expect(localStorage.length).toBe(0);
  });
});

describe('marketCountryOptions', () => {
  const markets = [
    { countryId: 'cze-id', isoCode: 'CZE', name: 'Czechia', translations: { cs: { name: 'Česko' } } },
    { countryId: 'svk-id', isoCode: 'SVK', name: 'Slovakia', translations: { cs: { name: 'Slovensko' } } },
  ];

  it('offers one option per market, valued by the country id and labelled in the language', () => {
    expect(marketCountryOptions(markets, 'cs')).toEqual([
      { label: 'Česko', value: 'cze-id' },
      { label: 'Slovensko', value: 'svk-id' },
    ]);
  });

  it('falls back from a missing translation to the name, and from a missing name to the code', () => {
    expect(marketCountryOptions(markets, 'uk')).toEqual([
      { label: 'Czechia', value: 'cze-id' },
      { label: 'Slovakia', value: 'svk-id' },
    ]);
    expect(
      marketCountryOptions([{ countryId: 'deu-id', isoCode: 'DEU', name: undefined, translations: undefined }], 'en'),
    ).toEqual([{ label: 'DEU', value: 'deu-id' }]);
  });

  // A market with no country id cannot be sent as an address country, so it is not offered.
  it('drops a market that names no country', () => {
    expect(
      marketCountryOptions([{ countryId: undefined, isoCode: 'POL', name: 'Poland', translations: undefined }], 'en'),
    ).toEqual([]);
  });
});
