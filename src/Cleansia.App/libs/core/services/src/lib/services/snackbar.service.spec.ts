import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';
import { SnackbarService } from './snackbar.service';

describe('SnackbarService.extractApiErrorMessage', () => {
  let service: SnackbarService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SnackbarService,
        { provide: MessageService, useValue: { add: jest.fn(), clear: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
      ],
    });
    service = TestBed.inject(SnackbarService);
  });

  it('returns the generic fallback for a nullish error', () => {
    expect(service.extractApiErrorMessage(null)).toBe(
      'api.common.error_occurred'
    );
  });

  it('uses the provided fallback key when no code is present', () => {
    expect(service.extractApiErrorMessage({}, 'api.order.failed')).toBe(
      'api.order.failed'
    );
  });

  it('returns the raw error detail when no mapping matches', () => {
    expect(
      service.extractApiErrorMessage({ result: { detail: 'Something broke' } })
    ).toBe('Something broke');
  });

  it('reads the detail from result.title when detail is absent', () => {
    expect(
      service.extractApiErrorMessage({ result: { title: 'Boom' } })
    ).toBe('Boom');
  });

  it('parses the detail from a JSON response string', () => {
    expect(
      service.extractApiErrorMessage({
        response: JSON.stringify({ detail: 'Parsed detail' }),
      })
    ).toBe('Parsed detail');
  });

  it('surfaces a non-JSON response string as the message', () => {
    expect(
      service.extractApiErrorMessage({ response: 'Internal Server Error' })
    ).toBe('Internal Server Error');
  });

  it('ignores an HTML response string and falls back', () => {
    expect(
      service.extractApiErrorMessage({ response: '<html>500</html>' })
    ).toBe('api.common.error_occurred');
  });

  it('falls back for a valid JSON response without a code', () => {
    expect(service.extractApiErrorMessage({ response: '{}' })).toBe(
      'api.common.error_occurred'
    );
  });

  it('falls back to error.message when no detail is found', () => {
    expect(
      service.extractApiErrorMessage({ message: 'raw message' })
    ).toBe('raw message');
  });

  it('maps a known normalized error code to its translation key', () => {
    const translated = 'After photos are required to complete the order';
    const translate = TestBed.inject(TranslateService);
    jest
      .spyOn(translate, 'instant')
      .mockImplementation((key: string | string[]) =>
        key === 'api.order.after_photos.required' ? translated : (key as string)
      );

    expect(
      service.extractApiErrorMessage({
        result: { detail: 'AfterPhotosRequiredToComplete' },
      })
    ).toBe(translated);
  });

  // The validation arm of CleansiaApiController puts the sentinel in `detail`
  // and the provider key only in `errors`; before the key won, the login screen
  // showed 'A validation problem occurred.' whenever this path beat the HTTP
  // interceptor to the snackbar.
  it('translates the provider key from errors instead of the validation sentinel', () => {
    const translated = 'This account signs in with Apple';
    const translate = TestBed.inject(TranslateService);
    jest
      .spyOn(translate, 'instant')
      .mockImplementation((key: string | string[]) =>
        key === 'api.auth.apple_type_error' ? translated : (key as string)
      );

    expect(
      service.extractApiErrorMessage({
        result: {
          detail: 'A validation problem occurred.',
          title: 'Validation Error',
          errors: { Email: 'auth.apple_type_error' },
        },
      })
    ).toBe(translated);
  });

  // The auth arm repeats the key in both places, so the new precedence must not
  // change what that surface renders.
  it('translates the auth key when detail and errors carry the same value', () => {
    const translated = 'This account signs in with Google';
    const translate = TestBed.inject(TranslateService);
    jest
      .spyOn(translate, 'instant')
      .mockImplementation((key: string | string[]) =>
        key === 'api.auth.google_type_error' ? translated : (key as string)
      );

    expect(
      service.extractApiErrorMessage({
        result: {
          detail: 'auth.google_type_error',
          title: 'Unauthorized',
          errors: { IdentityToken: 'auth.google_type_error' },
        },
      })
    ).toBe(translated);
  });
});
