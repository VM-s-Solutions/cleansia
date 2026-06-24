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
});
