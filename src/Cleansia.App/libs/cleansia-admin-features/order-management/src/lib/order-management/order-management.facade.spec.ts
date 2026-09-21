import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminCurrencyListItem,
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
