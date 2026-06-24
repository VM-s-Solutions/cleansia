import { extractApiErrorCode } from './api-error';

describe('extractApiErrorCode', () => {
  it('reads the code from result.detail', () => {
    expect(
      extractApiErrorCode({ result: { detail: 'order.invalid_status' } })
    ).toBe('order.invalid_status');
  });

  it('falls back to result.title when detail is absent', () => {
    expect(
      extractApiErrorCode({ result: { title: 'order.not_found' } })
    ).toBe('order.not_found');
  });

  it('prefers result.detail over result.title', () => {
    expect(
      extractApiErrorCode({
        result: { detail: 'order.invalid_status', title: 'order.not_found' },
      })
    ).toBe('order.invalid_status');
  });

  it('parses the code from a JSON response string when result is absent', () => {
    expect(
      extractApiErrorCode({
        response: JSON.stringify({ detail: 'refund.nothing_refundable' }),
      })
    ).toBe('refund.nothing_refundable');
  });

  it('parses the title from a JSON response string when detail is absent', () => {
    expect(
      extractApiErrorCode({
        response: JSON.stringify({ title: 'refund.failed' }),
      })
    ).toBe('refund.failed');
  });

  it('returns undefined for a non-JSON response string', () => {
    expect(
      extractApiErrorCode({ response: 'Internal Server Error' })
    ).toBeUndefined();
  });

  it('returns undefined for a JSON response without detail or title', () => {
    expect(extractApiErrorCode({ response: '{}' })).toBeUndefined();
  });

  it('returns undefined when no code is present', () => {
    expect(extractApiErrorCode({})).toBeUndefined();
    expect(extractApiErrorCode(null)).toBeUndefined();
    expect(extractApiErrorCode(undefined)).toBeUndefined();
  });
});
