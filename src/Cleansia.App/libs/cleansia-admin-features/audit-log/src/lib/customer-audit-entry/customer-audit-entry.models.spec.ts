import { buildPayloadView, formatPayloadJson } from './customer-audit-entry.models';

describe('buildPayloadView', () => {
  it('returns an empty view for a missing, blank or malformed payload', () => {
    expect(buildPayloadView(undefined)).toEqual({ rows: [], diff: [] });
    expect(buildPayloadView('   ')).toEqual({ rows: [], diff: [] });
    expect(buildPayloadView('{not json')).toEqual({ rows: [], diff: [] });
    expect(buildPayloadView('[1,2]')).toEqual({ rows: [], diff: [] });
  });

  it('flattens scalar members into key/value rows in the recorded order', () => {
    const view = buildPayloadView(
      JSON.stringify({ tier: 'plus', feeRate: 0.5, hasBeenAccepted: true, promoCodeId: null })
    );

    expect(view.rows).toEqual([
      { key: 'tier', value: 'plus' },
      { key: 'feeRate', value: '0.5' },
      { key: 'hasBeenAccepted', value: 'true' },
      { key: 'promoCodeId', value: null },
    ]);
    expect(view.diff).toEqual([]);
  });

  it('flattens a nested object into dotted keys and keeps an array as one JSON value', () => {
    const view = buildPayloadView(
      JSON.stringify({
        policyFigures: { freeHours: 24, partialRate: 0.25 },
        packageIds: ['pkg-1', 'pkg-2'],
      })
    );

    expect(view.rows).toEqual([
      { key: 'policyFigures.freeHours', value: '24' },
      { key: 'policyFigures.partialRate', value: '0.25' },
      { key: 'packageIds', value: '["pkg-1","pkg-2"]' },
    ]);
  });

  it('renders before/after members as a field diff instead of flat rows', () => {
    const view = buildPayloadView(
      JSON.stringify({
        before: { frequency: 'weekly', isActive: true },
        after: { frequency: 'biweekly', isActive: true },
        currencyCode: 'CZK',
      })
    );

    expect(view.rows).toEqual([{ key: 'currencyCode', value: 'CZK' }]);
    expect(view.diff).toEqual([
      { field: 'frequency', before: 'weekly', after: 'biweekly', changed: true },
      { field: 'isActive', before: 'true', after: 'true', changed: false },
    ]);
  });

  it('diffs a one-sided before/after (a create has no before, a delete no after)', () => {
    const view = buildPayloadView(JSON.stringify({ before: null, after: { frequency: 'weekly' } }));

    expect(view.rows).toEqual([]);
    expect(view.diff).toEqual([
      { field: 'frequency', before: null, after: 'weekly', changed: true },
    ]);
  });
});

describe('formatPayloadJson', () => {
  it('pretty-prints valid JSON and returns anything else verbatim', () => {
    expect(formatPayloadJson('{"a":1,"b":[1,2]}')).toBe('{\n  "a": 1,\n  "b": [\n    1,\n    2\n  ]\n}');
    expect(formatPayloadJson('{broken')).toBe('{broken');
    expect(formatPayloadJson(undefined)).toBe('');
  });
});
