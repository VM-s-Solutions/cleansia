import { cityNameMatches, isCityServiced } from './city-name-match';

/**
 * The same tables the server's `CityNameMatchTests.cs`, Android's `CityNameMatchTest.kt` and iOS's
 * `CityNameMatchTests.swift` pin, case for case. A port drifts by becoming STRICTER than the server,
 * which tells a customer we do not serve a city we do; four suites over one table make that red.
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
    expect(cityNameMatches('Praha', 'Praha\u00a08')).toBe(true);
    expect(cityNameMatches('Praha', 'Praha 4\u2011Chodov')).toBe(true);
  });

  it('serves a city part written after a dash from its city', () => {
    expect(cityNameMatches('Brno', 'Brno-střed')).toBe(true);
    expect(cityNameMatches('Brno', 'Brno-sever')).toBe(true);
    expect(cityNameMatches('Brno', 'Brno - Královo Pole')).toBe(true);
    expect(cityNameMatches('Praha', 'Praha-Kunratice')).toBe(true);
    expect(cityNameMatches('Praha', 'Praha - Smíchov')).toBe(true);
    expect(cityNameMatches('Ostrava', 'Ostrava-Poruba')).toBe(true);
    expect(cityNameMatches('Ostrava', 'Ostrava-Jih')).toBe(true);
    expect(cityNameMatches('Ústí nad Labem', 'Ústí nad Labem-Střekov')).toBe(true);
    expect(cityNameMatches('Ostrava', 'Ostrava 3 - Ostrava-Jih')).toBe(true);
  });

  it('serves a district numbered in Roman numerals from its city', () => {
    expect(cityNameMatches('Pardubice', 'Pardubice II')).toBe(true);
    expect(cityNameMatches('Liberec', 'Liberec XIV-Ruprechtice')).toBe(true);
  });

  it('serves a quarter of a district from the row naming that district', () => {
    expect(cityNameMatches('Praha 8', 'Praha 8 - Libeň')).toBe(true);
  });

  it('does not treat case or spacing as a difference', () => {
    expect(cityNameMatches('Praha', '  PRAHA  ')).toBe(true);
    expect(cityNameMatches('Hradec Králové', 'Hradec  Kralove')).toBe(true);
    expect(cityNameMatches('Ústí nad Labem', 'Ústí\u00a0nad\u00a0Labem')).toBe(true);
  });

  /** The rural ring AROUND a city is written like a city part, and is a different place. */
  it('refuses the okres around a city', () => {
    expect(cityNameMatches('Praha', 'Praha-západ')).toBe(false);
    expect(cityNameMatches('Praha', 'Praha-východ')).toBe(false);
    expect(cityNameMatches('Brno', 'Brno-venkov')).toBe(false);
    expect(cityNameMatches('Brno', 'Brno - venkov')).toBe(false);
    expect(cityNameMatches('Plzeň', 'Plzeň-sever')).toBe(false);
    expect(cityNameMatches('Plzeň', 'Plzeň-jih')).toBe(false);
  });

  it('refuses a different city', () => {
    expect(cityNameMatches('Praha', 'Nová Praha')).toBe(false);
    expect(cityNameMatches('Ústí nad Labem', 'Ústí nad Orlicí')).toBe(false);
    expect(cityNameMatches('Praha', 'Kladno')).toBe(false);
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
  const districts = (city: string) => Array.from({ length: 22 }, (_, i) => `${city} ${i + 1}`);
  const SERVICED = [
    ...['Praha', 'Prague', ...districts('Praha'), ...districts('Prague')].map((name) => ({ name, zipPrefix: '1' })),
    ...[
      'Brno',
      'Ostrava',
      'Plzeň',
      'Liberec',
      'Olomouc',
      'České Budějovice',
      'Hradec Králové',
      'Ústí nad Labem',
      'Pardubice',
    ].map((name) => ({ name, zipPrefix: undefined })),
  ];

  /**
   * Prague is matched by its postcode: a five-digit postcode decides a row with a ZIP prefix whatever
   * the city is called, and only an address without one falls back to the name.
   */
  it.each([
    [1, 'Praha', '110 00', true],
    [2, 'Прага', '110 00', true],
    [3, 'Prague', '15000', true],
    [4, 'Praha 4 - Chodov', '149 00', true],
    [5, 'PRAHA', '110 00', true],
    [6, 'Praha 8', '180\u00a000', true],
    [7, '', '120 00', true],
    [8, 'Praha', '252 42', false],
    [9, 'Praha', '602 00', false],
    [10, 'Praha', undefined, true],
    [11, 'Praha', '1100', true],
    [12, 'Praha-západ', '252 00', false],
    [13, 'Praha-západ', undefined, false],
    [14, 'Nová Praha', undefined, false],
    [15, 'Jesenice', '252 42', false],
    [16, 'Brno-střed', '602 00', true],
    [17, 'Brno - Královo Pole', '612 00', true],
    [18, 'Brno-venkov', '664 00', false],
    [19, 'Ostrava-Poruba', '708 00', true],
    [20, 'Pardubice II', '530 02', true],
    [21, 'Liberec XIV-Ruprechtice', '460 14', true],
    [22, 'Plzeň-jih', undefined, false],
    [23, 'Plzen', '301 00', true],
    [24, 'Pilsen', '301 00', false],
    [25, 'Ústí nad Labem-Střekov', '400 03', true],
    [26, 'Kladno', '272 01', false],
    [27, 'Brno', '110 00', true],
    [28, 'Praha 8 - Libeň', '180 00', true],
  ])('case %i: %p with postcode %p is serviced: %p', (_case, city, postcode, serviced) => {
    expect(isCityServiced(SERVICED, city, postcode)).toBe(serviced);
  });

  it('reads a blank postcode as no postcode', () => {
    expect(isCityServiced(SERVICED, 'Praha', '')).toBe(true);
    expect(isCityServiced(SERVICED, 'Praha', '   ')).toBe(true);
    expect(isCityServiced(SERVICED, 'Praha-západ', '')).toBe(false);
  });

  it('reads a postcode written with a no-break space', () => {
    expect(isCityServiced(SERVICED, 'Прага', '110\u00a000')).toBe(true);
    expect(isCityServiced(SERVICED, 'Praha', '602\u00a000')).toBe(false);
  });

  it('reads a postcode of any length but five digits as no postcode', () => {
    expect(isCityServiced(SERVICED, 'Praha', '6020')).toBe(true);
    expect(isCityServiced(SERVICED, 'Praha', '602000')).toBe(true);
    expect(isCityServiced(SERVICED, 'Praha', '602 000')).toBe(true);
  });

  /**
   * The row the repo's own seed actually contains: `insert_addresses.sql` seeds a customer at
   * `Plzen` while `insert_seed_data.sql` seeds the serviced row as `Plzeň`. The exact compare this
   * replaced rejected that address and blocked the booking the server would have accepted.
   */
  it('accepts the seeded address that the exact compare rejected', () => {
    expect(isCityServiced([{ name: 'Plzeň' }], 'Plzen', '301 00')).toBe(true);
  });

  it('serves nothing without a serviced city', () => {
    expect(isCityServiced([], 'Praha', '110 00')).toBe(false);
  });
});
