import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  DashboardStatsDto,
  OrderListItem,
  OrderStatus,
  PartnerClient,
} from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { MemoizedSelector, Store } from '@ngrx/store';
import { selectDashboardStats, selectUpcomingOrders } from '@cleansia/partner-stores';
import { EMPTY, of } from 'rxjs';
import { DashboardFacade } from './dashboard.facade';

describe('DashboardFacade', () => {
  let facade: DashboardFacade;
  let dispatch: jest.Mock;
  let stats: DashboardStatsDto | null;
  let upcoming: OrderListItem[];

  function upcomingOrderStatuses(): OrderStatus[] {
    const call = dispatch.mock.calls.find(
      ([action]) => action.filter?.orderStatuses
    );
    return call?.[0].filter.orderStatuses as OrderStatus[];
  }

  beforeEach(() => {
    dispatch = jest.fn();
    stats = null;
    upcoming = [];

    TestBed.configureTestingModule({
      providers: [
        DashboardFacade,
        {
          provide: Store,
          useValue: {
            dispatch,
            selectSignal: (selector: MemoizedSelector<object, unknown>) => () => {
              if (selector === selectDashboardStats) return stats;
              if (selector === selectUpcomingOrders) return upcoming;
              return null;
            },
          },
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
          useValue: { instant: (k: string) => k, currentLang: 'cs', onLangChange: EMPTY },
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

  describe('stat cards', () => {
    it('is empty until the stats arrive', () => {
      expect(facade.statCards()).toEqual([]);
    });

    // The server names the currency; the amount is written the way the session language writes
    // money, so the card and the my-pay ledger on the next page agree.
    it('prints the pending earnings as money in the session language', () => {
      stats = DashboardStatsDto.fromJS({
        availableOrdersCount: 1,
        myActiveOrdersCount: 2,
        thisMonthCompletedOrders: 9,
        lastMonthCompletedOrders: 0,
        currentPeriodEarnings: 1250,
        currencyCode: 'CZK',
      });
      const earnings = facade.statCards().find((card) => card.title === 'pages.dashboard.pending_earnings');
      expect(earnings?.value).toBe('1 250 Kč');
    });

    it('reads no trend when last month had nothing to compare against', () => {
      stats = DashboardStatsDto.fromJS({ thisMonthCompletedOrders: 9, lastMonthCompletedOrders: 0 });
      const completed = facade.statCards().find((card) => card.title === 'pages.dashboard.completed_this_month');
      expect(completed?.trend).toEqual({ value: 0, direction: 'neutral' });
    });
  });

  describe('upcoming order cards', () => {
    it('formats the cleaning stamp and the total in the session language', () => {
      upcoming = [
        OrderListItem.fromJS({
          id: 'o-1',
          displayOrderNumber: 'CZ-1',
          customerName: 'Jana',
          customerAddress: 'Ulice 1, Praha',
          cleaningDateTime: new Date(2026, 8, 21, 11, 0),
          totalPrice: 1250,
          currency: { code: 'CZK' },
          orderStatus: { name: 'New', value: OrderStatus.New },
        }),
      ];
      const [card] = facade.upcomingOrderCards();
      expect(card.id).toBe('o-1');
      expect(card.cleaningDate).toBe('21. 9. 2026 11:00');
      expect(card.totalPrice).toBe('1 250,00 Kč');
      expect(card.orderStatus).toEqual({ name: 'New', value: OrderStatus.New });
    });
  });
});
