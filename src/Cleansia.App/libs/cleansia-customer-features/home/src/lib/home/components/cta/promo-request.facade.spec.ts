import { TestBed } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { ApiErrorResult } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { PromoRequestFacade } from './promo-request.facade';

describe('PromoRequestFacade', () => {
  let facade: PromoRequestFacade;
  let request: jest.Mock;

  function alreadySent(): ApiErrorResult & { status: number } {
    // NSwag throws the parsed ProblemDetails directly for an HTTP 400.
    return {
      status: 400,
      title: 'Bad Request',
      detail: 'A validation problem occurred.',
      errors: { Email: 'promo.already_sent' },
    };
  }

  beforeEach(() => {
    request = jest.fn().mockReturnValue(of({ accepted: true }));
    TestBed.configureTestingModule({
      providers: [
        PromoRequestFacade,
        { provide: CustomerClient, useValue: { promoCodeClient: { request } } },
        { provide: TranslateService, useValue: { currentLang: 'cs' } },
      ],
    });
    facade = TestBed.inject(PromoRequestFacade);
  });

  it('reports acceptance and requests the promo in the current language', () => {
    facade.request('customer@example.com', true);

    expect(facade.isSent()).toBe(true);
    expect(facade.hasFailed()).toBe(false);
    expect(request).toHaveBeenCalledWith(
      expect.objectContaining({
        email: 'customer@example.com',
        languageCode: 'cs',
      })
    );
  });

  it('reports a duplicate as already sent instead of success or a retryable error', () => {
    request.mockReturnValue(throwError(alreadySent));

    facade.request('customer@example.com', true);

    expect(facade.isSent()).toBe(false);
    expect(facade.hasFailed()).toBe(true);
    expect(facade.failureMessageKey()).toBe('api.promo.already_sent');
  });

  it.each([
    new Error('Network unavailable'),
    { status: 400, errors: { Email: 'email.invalid' } },
    { status: 500, detail: 'Database exception' },
  ])(
    'keeps other failures generic without displaying server details',
    (error) => {
      request.mockReturnValue(throwError(() => error));

      facade.request('customer@example.com', true);

      expect(facade.hasFailed()).toBe(true);
      expect(facade.failureMessageKey()).toBe('pages.home.cta.promo_failed');
    }
  );

  it('clears duplicate feedback when a different address is submitted', () => {
    request.mockReturnValueOnce(throwError(alreadySent));
    facade.request('customer@example.com', true);

    request.mockReturnValueOnce(of({ accepted: false }));
    facade.request('other@example.com', true);

    expect(facade.hasFailed()).toBe(true);
    expect(facade.failureMessageKey()).toBe('pages.home.cta.promo_failed');
  });

  it('clears duplicate feedback on reset', () => {
    request.mockReturnValueOnce(throwError(alreadySent));
    facade.request('customer@example.com', true);

    facade.reset();

    expect(facade.state()).toBe('idle');
    expect(facade.hasFailed()).toBe(false);
    expect(facade.failureMessageKey()).toBe('pages.home.cta.promo_failed');
  });

  it('does not request without consent or an email address', () => {
    facade.request('customer@example.com', false);
    facade.request('', true);

    expect(request).not.toHaveBeenCalled();
    expect(facade.state()).toBe('idle');
  });

  it('does not send another request while a request is pending', () => {
    const response = new Subject<{ accepted: boolean }>();
    request.mockReturnValue(response);

    facade.request('customer@example.com', true);
    facade.request('customer@example.com', true);

    expect(request).toHaveBeenCalledTimes(1);
    expect(facade.isSending()).toBe(true);

    response.next({ accepted: true });
    response.complete();
    expect(facade.isSent()).toBe(true);
  });
});
