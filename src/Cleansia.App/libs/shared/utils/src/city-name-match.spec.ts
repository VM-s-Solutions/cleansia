import { cityNameMatches, isCityServiced } from './city-name-match';

const districts = (city: string) => Array.from({ length: 22 }, (_, i) => `${city} ${i + 1}`);

const FIXTURE_ROWS = [
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
  ].map((name) => ({ name, zipPrefix: null })),
];

/**
 * The serviced-city rule's shared case table: the same numbered cases, in the same order and with the same
 * answers, as the server's `CityNameMatchTests.cs`, Android's `CityNameMatchTest.kt` and iOS's
 * `CityNameMatchTests.swift`. A port drifts by becoming STRICTER than the server, which tells a customer we
 * do not serve a city we do; four suites over one table make that red.
 *
 * Change a case here and change it in all three twins. An absent city, postcode or row name is written
 * `null` and runs a second time as `undefined`, which this port reads alike.
 */
describe('the shared case table', () => {
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
    [10, 'Praha', null, true],
    [11, 'Praha', '1100', true],
    [12, 'Praha-západ', '252 00', false],
    [13, 'Praha-západ', null, false],
    [14, 'Nová Praha', null, false],
    [15, 'Jesenice', '252 42', false],
    [16, 'Brno-střed', '602 00', true],
    [17, 'Brno - Královo Pole', '612 00', true],
    [18, 'Brno-venkov', '664 00', false],
    [19, 'Ostrava-Poruba', '708 00', true],
    [20, 'Pardubice II', '530 02', true],
    [21, 'Liberec XIV-Ruprechtice', '460 14', true],
    [22, 'Plzeň-jih', null, false],
    [23, 'Plzen', '301 00', true],
    [24, 'Pilsen', '301 00', false],
    [25, 'Ústí nad Labem-Střekov', '400 03', true],
    [26, 'Kladno', '272 01', false],
    [27, 'Brno', '110 00', true],
    [28, 'Praha 8 - Libeň', '180 00', true],
    [29, 'Прага', '110\u00a000', true],
    [30, 'Praha', '602\u00a000', false],
    [31, 'Прага', '1100', false],
    [32, 'Praha', '6020', true],
    [33, 'Прага', '1abcd', false],
    [34, 'Praha', '252\u00a042', false],
    [35, 'Praha', '602000', true],
    [36, 'Praha', '602 000', true],
    [37, 'Praha', '110 000', true],
    [38, 'Прага', '110 000', false],
    [39, 'Praha', 'CZ-110 00', true],
    [40, 'Прага', 'CZ-110 00', false],
    [41, 'Praha', '110-00', true],
    [42, 'Прага', '110-00', false],
    [43, 'Praha', '', true],
    [44, 'Praha', '   ', true],
    [45, 'Прага', '', false],
    [46, 'Прага', '   ', false],
    [47, 'Praha-západ', '', false],
    [48, 'Nová Praha', '', false],
    [49, 'Plzeň-jih', '', false],
  ])('case %i: %p at %p is serviced: %p', (_case, city, postcode, serviced) => {
    expect(isCityServiced(FIXTURE_ROWS, city, postcode)).toBe(serviced);
    expect(isCityServiced(FIXTURE_ROWS, city, postcode ?? undefined)).toBe(serviced);
  });

  it.each([
    [50, 'Praha', '110 00', [], false],
    [51, 'Kladno', '110 00', [{ name: 'Praha', zipPrefix: ' 1 ' }], true],
    [52, 'Praha', '110 00', [{ name: 'Praha', zipPrefix: '1 1' }], false],
    [53, 'Praha', '252 42', [{ name: 'Praha', zipPrefix: ' ' }], true],
    [54, 'Прага', '110 00', [{ name: 'Praha', zipPrefix: '' }], false],
    [55, 'Plzen', '301 00', [{ name: 'Plzeň' }], true],
  ])('case %i: %p at %p against the rows %j is serviced: %p', (_case, city, postcode, rows, serviced) => {
    expect(isCityServiced(rows, city, postcode)).toBe(serviced);
  });

  it.each([
    [56, 'Plzeň', 'Plzen', true],
    [57, 'Plzen', 'Plzeň', true],
    [58, 'České Budějovice', 'Ceske Budejovice', true],
    [59, 'Ústí nad Labem', 'Usti nad Labem', true],
    [60, 'Hradec Králové', 'Hradec Kralove', true],
    [61, 'Praha', 'Praha 8', true],
    [62, 'Praha', 'Praha 22', true],
    [63, 'Prague', 'Prague 8', true],
    [64, 'Praha', 'Praha 4 - Chodov', true],
    [65, 'Praha', 'Praha 5 \u2013 Smíchov', true],
    [66, 'Praha', 'Praha 4-Chodov', true],
    [67, 'Praha', 'Praha 4\u2011Chodov', true],
    [68, 'Praha', 'Praha\u00a08', true],
    [69, 'Praha', 'Praha-Kunratice', true],
    [70, 'Praha', 'Praha - Smíchov', true],
    [71, 'Brno', 'Brno-střed', true],
    [72, 'Brno', 'Brno-sever', true],
    [73, 'Brno', 'Brno - Královo Pole', true],
    [74, 'Ostrava', 'Ostrava-Poruba', true],
    [75, 'Ostrava', 'Ostrava-Jih', true],
    [76, 'Ostrava', 'Ostrava 3 - Ostrava-Jih', true],
    [77, 'Ústí nad Labem', 'Ústí nad Labem-Střekov', true],
    [78, 'Pardubice', 'Pardubice II', true],
    [79, 'Liberec', 'Liberec XIV-Ruprechtice', true],
    [80, 'Plzeň', 'Plzeň 2-Slovany', true],
    [81, 'Praha 8', 'Praha 8 - Libeň', true],
    [82, 'Praha', 'Praha-', false],
    [83, 'Brno', 'Brno -', false],
    [84, 'Praha', '  PRAHA  ', true],
    [85, 'Hradec Králové', 'Hradec  Kralove', true],
    [86, 'Ústí nad Labem', 'Ústí\u00a0nad\u00a0Labem', true],
    [87, 'Praha', 'Praha-západ', false],
    [88, 'Praha', 'Praha-východ', false],
    [89, 'Brno', 'Brno-venkov', false],
    [90, 'Brno', 'Brno - venkov', false],
    [91, 'Plzeň', 'Plzeň-sever', false],
    [92, 'Plzeň', 'Plzeň-jih', false],
    [93, 'Praha', 'Nová Praha', false],
    [94, 'Ústí nad Labem', 'Ústí nad Orlicí', false],
    [95, 'Praha', 'Kladno', false],
    [96, 'Praha', 'Prague 8', false],
    [97, 'Praha', 'Prague', false],
    [98, 'Plzeň', 'Pilsen', false],
    [99, 'Praha', 'Прага', false],
    [100, 'Praha 8', 'Praha 22', false],
    [101, 'Praha 8', 'Praha', false],
    [102, 'Praha', '8', false],
    [103, 'Praha', '', false],
    [104, '', 'Praha', false],
    [105, 'Praha', ' ', false],
    [106, null, 'Praha', false],
    [107, 'Praha', null, false],
    [108, 'Praha', 'Praha', true],
    [109, 'Praha', 'PRAHA', true],
    [110, 'Praha', '  Praha ', true],
    [111, 'Brno', 'Brno', true],
    [112, 'Brno', 'BRNO', true],
    [113, 'Brno', '  Brno ', true],
    [114, 'Plzeň', 'Plzeň', true],
    [115, 'Plzeň', 'PLZEŇ', true],
    [116, 'Plzeň', '  Plzeň ', true],
    [117, 'České Budějovice', 'České Budějovice', true],
    [118, 'České Budějovice', 'ČESKÉ BUDĚJOVICE', true],
    [119, 'České Budějovice', '  České Budějovice ', true],
  ])('case %i: the row %p takes %p: %p', (_case, row, city, matches) => {
    expect(cityNameMatches(row, city)).toBe(matches);
    expect(cityNameMatches(row ?? undefined, city ?? undefined)).toBe(matches);
  });
});

/**
 * Not in the shared table: the characters the server reads, at each place this port reads them.
 * JavaScript's own `\s` and `trim()` lack U+0085 and add U+FEFF, its `\d` is ASCII, and NFD leaves marks
 * outside U+0300 to U+036F.
 */
describe('the characters the server reads', () => {
  it.each([
    ['a mark outside U+0300 to U+036F is removed', 'Praha', 'P\u1dc0raha', true],
    ['an enclosing mark is kept', 'Praha', 'Praha\u20dd', false],
    ['a district digit of another script counts', 'Brno', 'Brno \u0662', true],
    ['a Roman numeral sign is no district digit', 'Pardubice', 'Pardubice \u2161', false],
    ['U+0085 before a district is a space', 'Praha', 'Praha\u00858', true],
    ['U+FEFF before a district is no space', 'Praha', 'Praha\ufeff8', false],
    ['U+0085 is trimmed from a city', 'Praha', '\u0085Praha', true],
    ['U+FEFF is not trimmed from a city', 'Praha', '\ufeffPraha', false],
    ['U+FEFF is not trimmed from the part before a dash', 'Praha', 'Praha\ufeff-Libeň', false],
    ['U+FEFF is not trimmed from the part after a dash', 'Praha', 'Praha-\ufeff', true],
  ])('%s', (_reads, row, city, matches) => {
    expect(cityNameMatches(row, city)).toBe(matches);
  });

  it.each([
    ['U+0085 is removed from a postcode', 'Прага', '110\u008500', FIXTURE_ROWS, true],
    ['U+FEFF is not removed from a postcode, which is then not valid', 'Praha', '\ufeff602 00', FIXTURE_ROWS, true],
    ['a postcode in digits of another script is not valid', 'Praha', '\uff11\uff11\uff10\uff10\uff10', FIXTURE_ROWS, true],
    ['U+0085 is trimmed from a ZIP prefix', 'Kladno', '110 00', [{ name: 'Praha', zipPrefix: '\u00851' }], true],
    ['U+FEFF is not trimmed from a ZIP prefix', 'Kladno', '110 00', [{ name: 'Praha', zipPrefix: '\ufeff1' }], false],
  ])('%s', (_reads, city, postcode, rows, serviced) => {
    expect(isCityServiced(rows, city, postcode)).toBe(serviced);
  });
});
