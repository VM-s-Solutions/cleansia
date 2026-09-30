import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminReceivableClient,
  PagedDataOfReceivableListItem,
  ReceivableKind,
  ReceivableListItem,
  ReceivableStatus,
  SortDirection,
  WriteOffReceivableCommand,
  WriteOffReceivableResponse,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { ReceivablesFacade } from './receivables.facade';

function receivable(overrides: Record<string, unknown> = {}): ReceivableListItem {
  return ReceivableListItem.fromJS({
    id: 'receivable-1',
    orderId: 'order-1',
    displayOrderNumber: 'CL-1001',
    userId: 'user-1',
    kind: { value: ReceivableKind.Lockout, name: 'Lockout' },
    status: { value: ReceivableStatus.Open, name: 'Open' },
    amount: 1800,
    currencyCode: 'CZK',
    attempts: 0,
    createdOn: '2026-09-28T10:00:00Z',
    ...overrides,
  });
}

function page(data: ReceivableListItem[], total = data.length): PagedDataOfReceivableListItem {
  return PagedDataOfReceivableListItem.fromJS({ pageNumber: 1, pageSize: 20, total, data });
}

describe('ReceivablesFacade', () => {
  let facade: ReceivablesFacade;
  let getPaged: jest.Mock;
  let writeOff: jest.Mock;
  let navigate: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };

  const open = receivable();
  const paid = receivable({ id: 'receivable-2', status: { value: ReceivableStatus.Paid, name: 'Paid' } });

  beforeEach(() => {
    getPaged = jest.fn().mockReturnValue(of(page([open], 41)));
    writeOff = jest.fn().mockReturnValue(
      of(WriteOffReceivableResponse.fromJS({ receivableId: 'receivable-1', status: ReceivableStatus.WrittenOff }))
    );
    navigate = jest.fn();
    snackbar = { showSuccessTranslated: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ReceivablesFacade,
        { provide: AdminReceivableClient, useValue: { getPaged, writeOff } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: Router, useValue: { navigate } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en', onLangChange: new Subject() },
        },
      ],
    });

    facade = TestBed.inject(ReceivablesFacade);
  });

  describe('loadReceivables', () => {
    it('reads the first page of open receivables and settles the loading flags', () => {
      facade.loadReceivables();

      expect(getPaged).toHaveBeenCalledWith(
        ReceivableStatus.Open,
        undefined,
        undefined,
        undefined,
        undefined,
        0,
        20
      );
      expect(facade.receivables()).toEqual([open]);
      expect(facade.totalRecords()).toBe(41);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('leaves an empty list, not an error, when the company is owed nothing', () => {
      getPaged.mockReturnValue(of(page([])));

      facade.loadReceivables();

      expect(facade.receivables()).toEqual([]);
      expect(facade.totalRecords()).toBe(0);
      expect(facade.hasError()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
    });

    it('holds the loading flag while the read is in flight', () => {
      const pending = new Subject<PagedDataOfReceivableListItem>();
      getPaged.mockReturnValue(pending);

      facade.loadReceivables();

      expect(facade.loading()).toBe(true);
      expect(facade.initialLoading()).toBe(true);
      pending.next(page([open]));
      pending.complete();
      expect(facade.loading()).toBe(false);
    });

    it('settles the error state when the read fails, and clears it on the next good read', () => {
      getPaged.mockReturnValueOnce(throwError(() => new Error('boom')));

      facade.loadReceivables();

      expect(facade.hasError()).toBe(true);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);

      facade.loadReceivables();

      expect(facade.hasError()).toBe(false);
      expect(facade.receivables()).toEqual([open]);
    });
  });

  it('asks for every status once the filter is cleared, from the first page', () => {
    facade.onPageChange({ first: 40, rows: 20, page: 2, totalRecords: 41 });
    getPaged.mockClear();

    facade.selectStatus(null);

    expect(getPaged).toHaveBeenCalledWith(undefined, undefined, undefined, undefined, undefined, 0, 20);
  });

  it('pages and sorts on the server', () => {
    facade.onPageChange({ first: 20, rows: 20, page: 1, totalRecords: 41 });
    expect(getPaged).toHaveBeenLastCalledWith(ReceivableStatus.Open, undefined, undefined, undefined, undefined, 20, 20);

    facade.onSortChange({ field: 'amount', order: -1 });

    const sort = getPaged.mock.calls[getPaged.mock.calls.length - 1][4];
    expect(sort.map((s: { toJSON(): unknown }) => s.toJSON())).toEqual([
      { field: 'amount', direction: SortDirection.Descending },
    ]);
  });

  it('opens the order a receivable is owed on', () => {
    facade.viewOrder(open);

    expect(navigate).toHaveBeenCalledWith([CleansiaAdminRoute.ORDER_MANAGEMENT, 'order-1']);
  });

  describe('write-off', () => {
    beforeEach(() => facade.loadReceivables());

    it('starts only on an open receivable, with an empty note', () => {
      facade.startWriteOff(paid);
      expect(facade.writingOff()).toBeNull();

      facade.setWriteOffNote('left over');
      facade.startWriteOff(open);

      expect(facade.writingOff()).toBe(open);
      expect(facade.writeOffNote()).toBe('');
      expect(facade.canSubmitWriteOff()).toBe(false);
    });

    it('needs a note before it can be sent, and sends nothing without one', () => {
      facade.startWriteOff(open);
      facade.setWriteOffNote('   ');

      expect(facade.canSubmitWriteOff()).toBe(false);
      facade.writeOff();
      expect(writeOff).not.toHaveBeenCalled();
    });

    it('sends the receivable and the trimmed note, confirms, closes the panel and re-reads the list', () => {
      facade.startWriteOff(open);
      facade.setWriteOffNote('  customer moved abroad, not recoverable  ');
      getPaged.mockClear();

      facade.writeOff();

      const command: WriteOffReceivableCommand = writeOff.mock.calls[0][0];
      expect(command).toBeInstanceOf(WriteOffReceivableCommand);
      expect(command.toJSON()).toEqual({
        receivableId: 'receivable-1',
        note: 'customer moved abroad, not recoverable',
      });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.receivables.messages.write_off_success');
      expect(facade.writingOff()).toBeNull();
      expect(facade.submitting()).toBe(false);
      expect(getPaged).toHaveBeenCalledTimes(1);
    });

    it('keeps the panel open on a refusal and re-reads the list, which may have moved on', () => {
      writeOff.mockReturnValue(throwError(() => ({ result: { detail: 'receivable.not_open' } })));
      facade.startWriteOff(open);
      facade.setWriteOffNote('paid in the office');
      getPaged.mockClear();

      facade.writeOff();

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.writingOff()).toBe(open);
      expect(facade.writeOffNote()).toBe('paid in the office');
      expect(facade.submitting()).toBe(false);
      expect(getPaged).toHaveBeenCalledTimes(1);
    });

    it('ignores a second send while the first is in flight', () => {
      const pending = new Subject<WriteOffReceivableResponse>();
      writeOff.mockReturnValue(pending);
      facade.startWriteOff(open);
      facade.setWriteOffNote('uncollectable');

      facade.writeOff();
      facade.writeOff();

      expect(writeOff).toHaveBeenCalledTimes(1);
      expect(facade.canSubmitWriteOff()).toBe(false);
    });

    it('closes the panel when the status filter changes', () => {
      facade.startWriteOff(open);
      facade.setWriteOffNote('uncollectable');

      facade.selectStatus(ReceivableStatus.WrittenOff);

      expect(facade.writingOff()).toBeNull();
      expect(facade.writeOffNote()).toBe('');
    });
  });
});
