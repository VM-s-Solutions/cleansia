import { TestBed } from '@angular/core/testing';
import { AdminFiscalFailureClient, ApiClient } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { of, Subject } from 'rxjs';
import { FiscalFailuresListFacade } from './fiscal-failures-list.facade';

describe('FiscalFailuresListFacade', () => {
  let facade: FiscalFailuresListFacade;
  let retryMock: jest.Mock;
  let acknowledgeMock: jest.Mock;

  beforeEach(() => {
    retryMock = jest.fn();
    acknowledgeMock = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        FiscalFailuresListFacade,
        { provide: ApiClient, useValue: { adminFiscalFailure: jest.fn().mockReturnValue(of([])) } },
        {
          provide: AdminFiscalFailureClient,
          useValue: { retry: retryMock, acknowledge: acknowledgeMock },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccessTranslated: jest.fn(), showApiError: jest.fn() },
        },
      ],
    });

    facade = TestBed.inject(FiscalFailuresListFacade);
  });

  it.each([
    ['a retry', () => retryMock, (f: FiscalFailuresListFacade) => f.retryNow('r-1')],
    ['an acknowledge', () => acknowledgeMock, (f: FiscalFailuresListFacade) => f.acknowledge('r-1')],
  ])(
    'holds the list loading while %s is in flight, and clears it after it lands or fails',
    (_label, mock, act) => {
      const landing = new Subject<void>();
      mock().mockReturnValue(landing.asObservable());
      act(facade);
      expect(facade.loading()).toBe(true);
      landing.complete();
      expect(facade.loading()).toBe(false);

      const failing = new Subject<void>();
      mock().mockReturnValue(failing.asObservable());
      act(facade);
      expect(facade.loading()).toBe(true);
      failing.error(new Error('boom'));
      expect(facade.loading()).toBe(false);
    }
  );
});
