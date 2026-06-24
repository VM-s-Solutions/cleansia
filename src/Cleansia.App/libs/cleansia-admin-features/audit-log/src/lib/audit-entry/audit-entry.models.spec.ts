import { buildFieldDiff, parseSnapshot } from './audit-entry.models';

describe('parseSnapshot', () => {
  it('returns null for undefined, empty, and whitespace input', () => {
    expect(parseSnapshot(undefined)).toBeNull();
    expect(parseSnapshot('')).toBeNull();
    expect(parseSnapshot('   ')).toBeNull();
  });

  it('returns null for malformed JSON', () => {
    expect(parseSnapshot('{not json')).toBeNull();
  });

  it('returns null for a non-object JSON value', () => {
    expect(parseSnapshot('"a string"')).toBeNull();
    expect(parseSnapshot('[1, 2, 3]')).toBeNull();
  });

  it('parses an object snapshot', () => {
    expect(parseSnapshot('{"status":"New"}')).toEqual({ status: 'New' });
  });
});

describe('buildFieldDiff', () => {
  it('returns an empty diff when both snapshots are absent', () => {
    expect(buildFieldDiff(undefined, undefined)).toEqual([]);
    expect(buildFieldDiff('', '   ')).toEqual([]);
  });

  it('flags changed fields and leaves unchanged ones unflagged', () => {
    const diff = buildFieldDiff(
      '{"status":"New","note":"same"}',
      '{"status":"Confirmed","note":"same"}'
    );

    const status = diff.find((d) => d.field === 'status');
    const note = diff.find((d) => d.field === 'note');

    expect(status).toEqual({
      field: 'status',
      before: 'New',
      after: 'Confirmed',
      changed: true,
    });
    expect(note).toEqual({
      field: 'note',
      before: 'same',
      after: 'same',
      changed: false,
    });
  });

  it('renders an added field as before-null, after-value, changed', () => {
    const diff = buildFieldDiff('{"id":"o-1"}', '{"id":"o-1","reason":"fraud"}');
    const reason = diff.find((d) => d.field === 'reason');

    expect(reason).toEqual({
      field: 'reason',
      before: null,
      after: 'fraud',
      changed: true,
    });
  });

  it('renders a removed field as before-value, after-null, changed', () => {
    const diff = buildFieldDiff('{"id":"o-1","reason":"fraud"}', '{"id":"o-1"}');
    const reason = diff.find((d) => d.field === 'reason');

    expect(reason).toEqual({
      field: 'reason',
      before: 'fraud',
      after: null,
      changed: true,
    });
  });

  it('handles a create (no before) by treating every field as added', () => {
    const diff = buildFieldDiff(undefined, '{"id":"o-1","status":"New"}');

    expect(diff).toHaveLength(2);
    expect(diff.every((d) => d.before === null && d.changed)).toBe(true);
  });

  it('stringifies numbers, booleans, and nested objects', () => {
    const diff = buildFieldDiff(
      '{"count":1,"active":true,"meta":{"k":"v"}}',
      '{"count":2,"active":false,"meta":{"k":"w"}}'
    );

    expect(diff.find((d) => d.field === 'count')?.after).toBe('2');
    expect(diff.find((d) => d.field === 'active')?.after).toBe('false');
    expect(diff.find((d) => d.field === 'meta')?.after).toBe('{"k":"w"}');
  });

  it('sorts fields alphabetically for a stable render order', () => {
    const diff = buildFieldDiff('{"b":"1","a":"2"}', '{"b":"1","a":"3"}');
    expect(diff.map((d) => d.field)).toEqual(['a', 'b']);
  });
});
