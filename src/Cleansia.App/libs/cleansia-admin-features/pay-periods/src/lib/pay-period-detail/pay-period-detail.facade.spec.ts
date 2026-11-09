import { TestBed } from '@angular/core/testing';
import { AdminClient } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PayPeriodDetailFacade } from './pay-period-detail.facade';

describe('PayPeriodDetailFacade', () => {
  let facade: PayPeriodDetailFacade;
  let detailsMock: jest.Mock;

  beforeEach(() => {
    TestBed.resetTestingModule();
    detailsMock = jest.fn().mockReturnValue(of({ id: 'period-1' }));

    TestBed.configureTestingModule({
      providers: [
        PayPeriodDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPayPeriodClient: { details: detailsMock },
          },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(PayPeriodDetailFacade);
  });

  it('starts empty with nothing loading', () => {
    expect(facade.payPeriod()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('settles loading and holds nothing when the read fails', () => {
    detailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadPayPeriodDetail('period-1');

    expect(facade.payPeriod()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('renders an absent date as a dash', () => {
    expect(facade.formatDate(null)).toBe('-');
    expect(facade.formatDateTime(undefined)).toBe('-');
  });
});
