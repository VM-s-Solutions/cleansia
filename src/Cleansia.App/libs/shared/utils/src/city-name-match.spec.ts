import { cityNameMatches, isCityServiced } from './city-name-match';

/**
 * The same table the server's `CityNameMatchTests.cs`, Android's `CityNameMatchTest.kt` and iOS's
 * `CityNameMatchTests.swift` pin, case for case and in the same order so a diff between the four
 * is readable.
 *
 * **The duplication is the point.** This is a port of a server rule, and a port fails by drifting —
 * specifically by becoming STRICTER than the server, which tells a customer we do not serve a city
 * we do serve. Four suites over one table means a divergence reddens something.
 *
 * Change a case here and change it in all three twins.
 */
describe('cityNameMatches', () => {
  it('treats a spelling without diacritics as the same city', () => {
    expect(cityNameMatches('Plzeň', 'Plzen')).toBe(true);
    expect(cityNameMatches('Plzen', 'Plzeň')).toBe(true);
    expect(cityNameMatches('České Budějovice', 'Ceske Budejovice')).toBe(true);
    expect(cityNameMatches('Ústí nad Labem', 'Usti nad Labem')).toBe(true);
    expect(cityNameMatches('Hradec Králové', 'Hradec Kralove')).toBe(true);
  });

  it('serves a district from its city', () => {
    expect(cityNameMatches('Praha', 'Praha 8')).toBe(true);
    expect(cityNameMatches('Praha', 'Praha 22')).toBe(true);
    expect(cityNameMatches('Prague', 'Prague 8')).toBe(true);
    expect(cityNameMatches('Praha', 'Praha 4 - Chodov')).toBe(true);
    expect(cityNameMatches('Praha', 'Praha 5 – Smíchov')).toBe(true);
    expect(cityNameMatches('Praha', 'Praha 4-Chodov')).toBe(true);
  });

  it('does not treat case or spacing as a difference', () => {
    expect(cityNameMatches('Praha', '  PRAHA  ')).toBe(true);
    expect(cityNameMatches('Hradec Králové', 'Hradec  Kralove')).toBe(true);
  });

  /** The rural ring AROUND a city is a different place from the city. */
  it('refuses the okres around a city', () => {
    expect(cityNameMatches('Praha', 'Praha-západ')).toBe(false);
    expect(cityNameMatches('Praha', 'Praha-východ')).toBe(false);
    expect(cityNameMatches('Brno', 'Brno-venkov')).toBe(false);
  });

  it('refuses a different city', () => {
    expect(cityNameMatches('Praha', 'Nová Praha')).toBe(false);
    expect(cityNameMatches('Ústí nad Labem', 'Ústí nad Orlicí')).toBe(false);
    expect(cityNameMatches('Praha', 'Kladno')).toBe(false);
    expect(cityNameMatches('Brno', 'Brno-střed')).toBe(false);
  });

  /**
   * Deliberate: an exonym is DATA, not a rule. The seed carries `Prague` and `Prague 1..22` as
   * their own rows precisely because the geocoder returns the UI language's name.
   */
  it('matches no exonym without its own row', () => {
    expect(cityNameMatches('Praha', 'Prague 8')).toBe(false);
    expect(cityNameMatches('Praha', 'Prague')).toBe(false);
    expect(cityNameMatches('Plzeň', 'Pilsen')).toBe(false);
    expect(cityNameMatches('Praha', 'Прага')).toBe(false);
  });

  /** An operator who seeded one district meant one district. */
  it('does not let a row naming one district claim the city', () => {
    expect(cityNameMatches('Praha 8', 'Praha 22')).toBe(false);
    expect(cityNameMatches('Praha 8', 'Praha')).toBe(false);
  });

  it('matches nothing against nothing', () => {
    expect(cityNameMatches('Praha', '8')).toBe(false);
    expect(cityNameMatches('Praha', '')).toBe(false);
    expect(cityNameMatches('', 'Praha')).toBe(false);
    expect(cityNameMatches('Praha', ' ')).toBe(false);
    expect(cityNameMatches(null, 'Praha')).toBe(false);
    expect(cityNameMatches('Praha', null)).toBe(false);
    expect(cityNameMatches(undefined, 'Praha')).toBe(false);
    expect(cityNameMatches('Praha', undefined)).toBe(false);
  });
});

describe('isCityServiced', () => {
  const serviced = ['Praha', 'Brno', 'Plzeň'];

  it('answers over the whole list', () => {
    expect(isCityServiced(serviced, 'Praha 4 - Chodov')).toBe(true);
    expect(isCityServiced(serviced, 'Plzen')).toBe(true);
    expect(isCityServiced(serviced, 'Kladno')).toBe(false);
    expect(isCityServiced([], 'Praha')).toBe(false);
  });

  /**
   * The row the repo's own seed actually contains: `insert_addresses.sql` seeds a customer at
   * `Plzen` while `insert_seed_data.sql` seeds the serviced row as `Plzeň`. The exact compare this
   * replaced rejected that address and blocked the booking the server would have accepted.
   */
  it('accepts the seeded address that the exact compare rejected', () => {
    expect(isCityServiced(['Plzeň'], 'Plzen')).toBe(true);
  });
});
