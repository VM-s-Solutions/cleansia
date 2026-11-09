import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  CreatePayPeriodCommand,
  CreatePayPeriodResponse,
  PagedDataOfPayPeriodDto,
  PayPeriodDto,
  PayPeriodStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, Subject, throwError } from 'rxjs';
import { PayPeriodManagementFacade } from './pay-period-management.facade';

describe('PayPeriodManagementFacade', () => {
  let facade: PayPeriodManagementFacade;
  let getPagedMock: jest.Mock;
  let createMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getPagedMock = jest.fn().mockReturnValue(of({ data: [], total: 0 }));
    createMock = jest.fn().mockReturnValue(of(CreatePayPeriodResponse.fromJS({ payPeriodId: 'period-new' })));
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        PayPeriodManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPayPeriodClient: { getPaged: getPagedMock, create: createMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
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

    facade = TestBed.inject(PayPeriodManagementFacade);
  });

  it('starts empty and initially loading', () => {
    expect(facade.payPeriods()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.initialLoading()).toBe(true);
  });

  it('drops the initial-loading latch on an empty page', () => {
    facade.loadPayPeriods();

    expect(facade.payPeriods()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('drops the initial-loading latch when the page read fails', () => {
    getPagedMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadPayPeriods();

    expect(facade.payPeriods()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('holds the page and the total once a read lands', () => {
    getPagedMock.mockReturnValue(of({ data: [{ id: 'p-1' }], total: 7 }));

    facade.loadPayPeriods();

    expect(facade.payPeriods()).toEqual([{ id: 'p-1' }]);
    expect(facade.totalRecords()).toBe(7);
  });

  it('sends the filter to the server and returns to the first page', () => {
    facade.onPageChange({ first: 40, rows: 20, page: 2, totalRecords: 100 });
    facade.applyFilter({ status: PayPeriodStatus.Closed, year: 2026 });

    const [status, year, , offset] = getPagedMock.mock.calls.at(-1) ?? [];
    expect(status).toBe(PayPeriodStatus.Closed);
    expect(year).toBe(2026);
    expect(offset).toBe(0);
  });

  it('clears the filter on reset', () => {
    facade.applyFilter({ status: PayPeriodStatus.Closed, year: 2026 });
    facade.applyFilter({});

    const [status, year] = getPagedMock.mock.calls.at(-1) ?? [];
    expect(status).toBeUndefined();
    expect(year).toBeUndefined();
  });

  describe('create dialog default', () => {
    const calendarDay = (date: Date | null) =>
      date ? [date.getFullYear(), date.getMonth() + 1, date.getDate()] : null;
    const latestEndingOn = (endDate: string) =>
      of(PagedDataOfPayPeriodDto.fromJS({ data: [{ id: 'p-latest', endDate }], total: 1 }));

    afterEach(() => jest.useRealTimers());

    it('reads the one period with the latest end, whatever the list shows', () => {
      facade.applyFilter({ status: PayPeriodStatus.Paid, year: 2025 });
      getPagedMock.mockClear();

      facade.prepareCreate();

      expect(getPagedMock).toHaveBeenCalledWith(
        undefined,
        undefined,
        [new SortDefinition({ field: 'endDate', direction: SortDirection.Descending })],
        0,
        1
      );
    });

    it('defaults the start to the day after the latest end', () => {
      jest.useFakeTimers({ now: new Date(2026, 10, 9, 12) });
      getPagedMock.mockReturnValue(latestEndingOn('2026-10-31'));

      facade.prepareCreate();

      expect(calendarDay(facade.createStartDate())).toEqual([2026, 11, 1]);
    });

    it('defaults to today while the latest period runs past it, since the server refuses a later start', () => {
      jest.useFakeTimers({ now: new Date(2026, 10, 9, 12) });
      getPagedMock.mockReturnValue(latestEndingOn('2026-11-14'));

      facade.prepareCreate();

      expect(calendarDay(facade.createStartDate())).toEqual([2026, 11, 9]);
    });

    it('leaves the start empty when there is no period yet or the read fails', () => {
      facade.setCreateStartDate(new Date(2026, 0, 1));

      facade.prepareCreate();
      expect(facade.createStartDate()).toBeNull();

      getPagedMock.mockReturnValue(throwError(() => new Error('boom')));
      facade.prepareCreate();
      expect(facade.createStartDate()).toBeNull();
    });

    it('keeps a start the admin picked before the read lands', () => {
      const response$ = new Subject<PagedDataOfPayPeriodDto>();
      getPagedMock.mockReturnValue(response$);

      facade.prepareCreate();
      facade.setCreateStartDate(new Date(2026, 10, 3));
      response$.next(
        PagedDataOfPayPeriodDto.fromJS({ data: [PayPeriodDto.fromJS({ endDate: '2026-10-31' })], total: 1 })
      );

      expect(calendarDay(facade.createStartDate())).toEqual([2026, 11, 3]);
    });
  });

  describe('create', () => {
    it('sends only the start on the wire, since the server sets the end at the configured length', () => {
      facade.setCreateStartDate(new Date(2026, 9, 19));

      facade.createPayPeriod(jest.fn());

      const command: CreatePayPeriodCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreatePayPeriodCommand);
      expect(command.toJSON()).toEqual({
        startDate: '2026-10-19',
        notes: undefined,
      });
    });

    it('toasts, closes and re-reads once the create lands', () => {
      facade.setCreateStartDate(new Date(2026, 9, 19));
      const onSuccess = jest.fn();

      facade.createPayPeriod(onSuccess);

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pay_periods.messages.create_success');
      expect(onSuccess).toHaveBeenCalledTimes(1);
      expect(getPagedMock).toHaveBeenCalledTimes(1);
      expect(facade.creating()).toBe(false);
    });

    it('keeps the dialog open and the list as it was when the create is refused', () => {
      createMock.mockReturnValue(throwError(() => ({ result: { detail: 'pay_period.overlapping_period' } })));
      facade.setCreateStartDate(new Date(2026, 9, 19));
      const onSuccess = jest.fn();

      facade.createPayPeriod(onSuccess);

      expect(onSuccess).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(getPagedMock).not.toHaveBeenCalled();
      expect(facade.creating()).toBe(false);
    });

    it('sends nothing without a start date', () => {
      const onSuccess = jest.fn();

      facade.createPayPeriod(onSuccess);

      expect(createMock).not.toHaveBeenCalled();
      expect(onSuccess).not.toHaveBeenCalled();
    });

    it('holds creating while the request is in flight', () => {
      const response$ = new Subject<CreatePayPeriodResponse>();
      createMock.mockReturnValue(response$);
      facade.setCreateStartDate(new Date(2026, 9, 19));

      facade.createPayPeriod(jest.fn());
      expect(facade.creating()).toBe(true);

      response$.next(CreatePayPeriodResponse.fromJS({ payPeriodId: 'period-new' }));
      response$.complete();
      expect(facade.creating()).toBe(false);
    });

    it('sends one create while one is in flight', () => {
      createMock.mockReturnValue(new Subject<CreatePayPeriodResponse>());
      facade.setCreateStartDate(new Date(2026, 9, 19));

      facade.createPayPeriod(jest.fn());
      facade.createPayPeriod(jest.fn());

      expect(createMock).toHaveBeenCalledTimes(1);
    });
  });
});
