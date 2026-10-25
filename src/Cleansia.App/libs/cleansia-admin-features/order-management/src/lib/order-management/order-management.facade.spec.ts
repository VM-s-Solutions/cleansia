import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminCurrencyListItem,
  OrderStatus,
  PagedDataOfOrderListItem,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of } from 'rxjs';
import { OrderManagementFacade } from './order-management.facade';

describe('OrderManagementFacade', () => {
  let facade: OrderManagementFacade;
  let getPagedMock: jest.Mock;
  let getOverviewMock: jest.Mock;

  const CURRENCY_SLOT = 17;

  beforeEach(() => {
    getPagedMock = jest
      .fn()
      .mockReturnValue(of(PagedDataOfOrderListItem.fromJS({ data: [], total: 0 })));
    getOverviewMock = jest.fn().mockReturnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        OrderManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminOrderClient: { getPaged: getPagedMock },
            adminCurrencyClient: { getOverview: getOverviewMock },
          },
        },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) => k,
            currentLang: 'cs',
            onLangChange: EMPTY,
          },
        },
      ],
    });

    facade = TestBed.inject(OrderManagementFacade);
  });

  it('passes the chosen currency to the list in its own slot, and clears it on reset', () => {
    facade.applyFilter({ currencyId: 'cur-eur' });

    expect(getPagedMock.mock.lastCall?.[CURRENCY_SLOT]).toBe('cur-eur');

    facade.applyFilter({});

    expect(getPagedMock).toHaveBeenCalledTimes(2);
    expect(getPagedMock.mock.lastCall?.[CURRENCY_SLOT]).toBeUndefined();
  });

  it('sends no currency when the filter names none', () => {
    facade.loadOrders();

    expect(getPagedMock.mock.lastCall?.[CURRENCY_SLOT]).toBeUndefined();
  });

  describe('the staffing filter', () => {
    const DATE_FROM_SLOT = 7;
    const ORDER_STATUSES_SLOT = 13;
    const IS_UNASSIGNED_SLOT = 15;

    beforeEach(() => jest.useFakeTimers().setSystemTime(new Date(2026, 9, 24, 15, 30)));
    afterEach(() => jest.useRealTimers());

    it('asks the list for orders with nobody on them once the box is ticked, and names it in a chip', () => {
      facade.filterForm.patchValue({ unassignedOnly: true });
      jest.advanceTimersByTime(500);

      const args = getPagedMock.mock.lastCall;
      expect(args?.[IS_UNASSIGNED_SLOT]).toBe(true);
      expect(args?.[ORDER_STATUSES_SLOT]).toEqual([
        OrderStatus.New,
        OrderStatus.Confirmed,
        OrderStatus.OnTheWay,
        OrderStatus.InProgress,
      ]);
      expect(args?.[DATE_FROM_SLOT]).toEqual(new Date(2026, 9, 24));
      expect(facade.filters.chips()).toEqual([
        {
          key: 'unassignedOnly',
          label: 'pages.order_management.filters.staffing',
          value: 'pages.order_management.filters.no_cleaner',
        },
      ]);
    });

    it('asks nothing about the crew again once the chip is removed', () => {
      facade.filterForm.patchValue({ unassignedOnly: true });
      jest.advanceTimersByTime(500);

      facade.filters.removeChip('unassignedOnly');

      expect(facade.filterForm.value.unassignedOnly).toBe(false);
      expect(getPagedMock.mock.lastCall?.[IS_UNASSIGNED_SLOT]).toBeUndefined();
      expect(getPagedMock.mock.lastCall?.[ORDER_STATUSES_SLOT]).toBeUndefined();
      expect(facade.filters.chips()).toEqual([]);
    });
  });

  it('keeps only currencies that carry an id and a code', () => {
    getOverviewMock.mockReturnValue(
      of([
        AdminCurrencyListItem.fromJS({ id: 'cur-czk', code: 'CZK', isDefault: true }),
        AdminCurrencyListItem.fromJS({ id: 'cur-eur', code: 'EUR', isDefault: false }),
        AdminCurrencyListItem.fromJS({ id: undefined, code: 'XXX' }),
        AdminCurrencyListItem.fromJS({ id: 'cur-blank', code: undefined }),
      ])
    );

    facade.loadCurrencies();

    expect(facade.currencies()).toEqual([
      { id: 'cur-czk', code: 'CZK', isDefault: true },
      { id: 'cur-eur', code: 'EUR', isDefault: false },
    ]);
  });
});
