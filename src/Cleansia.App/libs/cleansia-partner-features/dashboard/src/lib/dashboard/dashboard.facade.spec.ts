import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { OrderStatus, PartnerClient } from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { Store } from '@ngrx/store';
import { of } from 'rxjs';
import { DashboardFacade } from './dashboard.facade';

describe('DashboardFacade', () => {
  let facade: DashboardFacade;
  let dispatch: jest.Mock;

  function upcomingOrderStatuses(): OrderStatus[] {
    const call = dispatch.mock.calls.find(
      ([action]) => action.filter?.orderStatuses
    );
    return call?.[0].filter.orderStatuses as OrderStatus[];
  }

  beforeEach(() => {
    dispatch = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        DashboardFacade,
        {
          provide: Store,
          useValue: { dispatch, selectSignal: () => () => null },
        },
        {
          provide: PartnerClient,
          useValue: {
            employeeClient: {
              getCurrentEmployee: () => of({ id: 'emp-1' }),
            },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'en' },
        },
      ],
    });

    facade = TestBed.inject(DashboardFacade);
  });

  it('loads the upcoming list with the statuses of a job in flight', () => {
    expect(facade).toBeTruthy();

    expect(upcomingOrderStatuses()).toEqual([
      OrderStatus.Confirmed,
      OrderStatus.OnTheWay,
      OrderStatus.InProgress,
    ]);
  });

  it('keeps a job on the dashboard after the cleaner reports being on the way', () => {
    expect(upcomingOrderStatuses()).toContain(OrderStatus.OnTheWay);
  });

  it('does not ask for the dead Pending status', () => {
    expect(upcomingOrderStatuses()).not.toContain(OrderStatus.Pending);
  });
});
