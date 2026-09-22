import { TranslateService } from '@ngx-translate/core';
import { extractApiErrorCode, resolveApiErrorKey } from './api-error';

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

  it('prefers the first result.errors value over the detail sentinel', () => {
    expect(
      extractApiErrorCode({
        result: {
          detail: 'A validation problem occurred.',
          title: 'Validation Error',
          errors: { Email: 'auth.apple_type_error' },
        },
      })
    ).toBe('auth.apple_type_error');
  });

  it('reads the first entry of a string[] errors value', () => {
    expect(
      extractApiErrorCode({
        result: {
          detail: 'A validation problem occurred.',
          errors: { Email: ['auth.apple_type_error', 'auth.account_locked'] },
        },
      })
    ).toBe('auth.apple_type_error');
  });

  it('falls back to result.detail for an empty errors object', () => {
    expect(
      extractApiErrorCode({
        result: { detail: 'order.invalid_status', errors: {} },
      })
    ).toBe('order.invalid_status');
  });

  it('skips blank errors values and falls back to result.detail', () => {
    expect(
      extractApiErrorCode({
        result: { detail: 'order.invalid_status', errors: { Email: '' } },
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

  it('prefers the errors value over the detail sentinel in a JSON response string', () => {
    expect(
      extractApiErrorCode({
        response: JSON.stringify({
          detail: 'A validation problem occurred.',
          title: 'Validation Error',
          errors: { Email: 'auth.google_type_error' },
        }),
      })
    ).toBe('auth.google_type_error');
  });

  it('falls back to the detail of a JSON response string with an empty errors object', () => {
    expect(
      extractApiErrorCode({
        response: JSON.stringify({
          detail: 'refund.nothing_refundable',
          errors: {},
        }),
      })
    ).toBe('refund.nothing_refundable');
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

  // NSwag's `throwException` throws the parsed ProblemDetails BARE whenever the
  // response carried a body, so this — not the `.result` wrapper — is the shape
  // every 400 and 401 from the generated clients actually arrives in.
  describe('bare ProblemDetails, as the generated clients throw it', () => {
    it('reads the code from a top-level detail', () => {
      expect(extractApiErrorCode({ detail: 'order.not_found' })).toBe(
        'order.not_found'
      );
    });

    it('prefers a top-level errors value over the detail sentinel', () => {
      expect(
        extractApiErrorCode({
          detail: 'A validation problem occurred.',
          title: 'Validation Error',
          errors: { Email: 'auth.apple_type_error' },
        })
      ).toBe('auth.apple_type_error');
    });

    it('falls back to a top-level title', () => {
      expect(extractApiErrorCode({ title: 'auth.invalid_apple_token' })).toBe(
        'auth.invalid_apple_token'
      );
    });

    it('still prefers the wrapper when both shapes are present', () => {
      expect(
        extractApiErrorCode({
          detail: 'order.not_found',
          result: { detail: 'auth.account_locked' },
        })
      ).toBe('auth.account_locked');
    });
  });
});

describe('resolveApiErrorKey', () => {
  const translate = {
    instant: (key: string) => (key === 'api.order.not_found' || key === 'api.common.error_occurred' ? `[${key}]` : key),
  } as unknown as TranslateService;

  it('maps a translated business code to its api.* key', () => {
    expect(resolveApiErrorKey(translate, { result: { detail: 'order.not_found' } })).toBe('api.order.not_found');
  });

  it('falls back to the generic key when the code has no translation', () => {
    expect(resolveApiErrorKey(translate, { result: { detail: 'something.unknown' } })).toBe(
      'api.common.error_occurred'
    );
  });

  it('falls back to the caller-named key when given one', () => {
    expect(resolveApiErrorKey(translate, { result: { detail: 'something.unknown' } }, 'api.refund.failed')).toBe(
      'api.refund.failed'
    );
  });

  it('falls back when the error carries no code at all', () => {
    expect(resolveApiErrorKey(translate, new Error('network'))).toBe('api.common.error_occurred');
  });
});
