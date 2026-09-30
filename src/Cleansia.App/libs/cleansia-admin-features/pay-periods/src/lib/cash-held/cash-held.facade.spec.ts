import { TestBed } from '@angular/core/testing';
import {
  AdminCashHeldClient,
  CleanerCashHeldDto,
  RecordCashRemittanceCommand,
  RecordCashRemittanceResponse,
  WriteOffCashHeldCommand,
  WriteOffCashHeldResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { CashHeldFacade } from './cash-held.facade';
import { CashHeldActionKind } from './cash-held.models';

describe('CashHeldFacade', () => {
  let facade: CashHeldFacade;
  let getAll: jest.Mock;
  let recordRemittance: jest.Mock;
  let writeOff: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };

  const jana = CleanerCashHeldDto.fromJS({
    employeeId: 'employee-1',
    employeeName: 'Jana Nováková',
    currencyId: 'currency-czk',
    currencyCode: 'CZK',
    amount: 2450.5,
  });

  beforeEach(() => {
    getAll = jest.fn().mockReturnValue(of([jana]));
    recordRemittance = jest.fn().mockReturnValue(of(RecordCashRemittanceResponse.fromJS({ id: 'entry-1' })));
    writeOff = jest.fn().mockReturnValue(of(WriteOffCashHeldResponse.fromJS({ id: 'entry-2' })));
    snackbar = { showSuccessTranslated: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        CashHeldFacade,
        { provide: AdminCashHeldClient, useValue: { getAll, recordRemittance, writeOff } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en', onLangChange: new Subject() },
        },
      ],
    });

    facade = TestBed.inject(CashHeldFacade);
  });

  describe('loadCashHeld', () => {
    it('reads every cleaner holding cash and settles the loading flags', () => {
      facade.loadCashHeld();

      expect(facade.cashHeld()).toEqual([jana]);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('leaves an empty list, not an error, when no cleaner holds cash', () => {
      getAll.mockReturnValue(of([]));

      facade.loadCashHeld();

      expect(facade.cashHeld()).toEqual([]);
      expect(facade.hasError()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
    });

    it('holds the loading flag while the read is in flight', () => {
      const pending = new Subject<CleanerCashHeldDto[]>();
      getAll.mockReturnValue(pending);

      facade.loadCashHeld();

      expect(facade.loading()).toBe(true);
      pending.next([jana]);
      pending.complete();
      expect(facade.loading()).toBe(false);
    });

    it('settles the error state when the read fails, and clears it on the next good read', () => {
      getAll.mockReturnValueOnce(throwError(() => new Error('boom')));

      facade.loadCashHeld();

      expect(facade.hasError()).toBe(true);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);

      facade.loadCashHeld();

      expect(facade.hasError()).toBe(false);
      expect(facade.cashHeld()).toEqual([jana]);
    });
  });

  describe('record a remittance', () => {
    it('opens with the whole balance as the amount and needs no note', () => {
      facade.startRemittance(jana);

      expect(facade.action()).toEqual({ kind: CashHeldActionKind.Remittance, row: jana });
      expect(facade.amount()).toBe('2450.5');
      expect(facade.canSubmit()).toBe(true);
    });

    it('refuses an amount that is not a positive number before asking the server', () => {
      facade.startRemittance(jana);

      for (const amount of ['', '0', '-10', 'abc']) {
        facade.setAmount(amount);
        expect(facade.canSubmit()).toBe(false);
        facade.submit();
      }

      expect(recordRemittance).not.toHaveBeenCalled();
    });

    it('sends the cleaner, the currency, the amount and no note when none is typed, then re-reads the list', () => {
      facade.startRemittance(jana);
      facade.setAmount('1000');
      facade.setNote('   ');
      getAll.mockClear();

      facade.submit();

      const command: RecordCashRemittanceCommand = recordRemittance.mock.calls[0][0];
      expect(command).toBeInstanceOf(RecordCashRemittanceCommand);
      expect(command.toJSON()).toEqual({
        employeeId: 'employee-1',
        currencyId: 'currency-czk',
        amount: 1000,
        note: undefined,
      });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.cash_held.messages.remittance_success');
      expect(facade.action()).toBeNull();
      expect(getAll).toHaveBeenCalledTimes(1);
      expect(writeOff).not.toHaveBeenCalled();
    });
  });

  describe('write off', () => {
    it('needs a note before it can be sent', () => {
      facade.startWriteOff(jana);

      expect(facade.canSubmit()).toBe(false);
      facade.submit();
      expect(writeOff).not.toHaveBeenCalled();

      facade.setNote('cleaner left the platform, amount unrecoverable');
      expect(facade.canSubmit()).toBe(true);
    });

    it('sends the cleaner, the currency, the amount and the trimmed note, then re-reads the list', () => {
      facade.startWriteOff(jana);
      facade.setAmount('450.5');
      facade.setNote('  stolen from the car, police report 12/2026  ');
      getAll.mockClear();

      facade.submit();

      const command: WriteOffCashHeldCommand = writeOff.mock.calls[0][0];
      expect(command).toBeInstanceOf(WriteOffCashHeldCommand);
      expect(command.toJSON()).toEqual({
        employeeId: 'employee-1',
        currencyId: 'currency-czk',
        amount: 450.5,
        note: 'stolen from the car, police report 12/2026',
      });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.cash_held.messages.write_off_success');
      expect(facade.action()).toBeNull();
      expect(getAll).toHaveBeenCalledTimes(1);
      expect(recordRemittance).not.toHaveBeenCalled();
    });

    it('keeps the panel and what was typed on a refusal, and re-reads the balance', () => {
      writeOff.mockReturnValue(throwError(() => ({ result: { detail: 'cash_held.amount_exceeds_balance' } })));
      facade.startWriteOff(jana);
      facade.setAmount('9999');
      facade.setNote('uncollectable');
      getAll.mockClear();

      facade.submit();

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.action()?.kind).toBe(CashHeldActionKind.WriteOff);
      expect(facade.amount()).toBe('9999');
      expect(facade.note()).toBe('uncollectable');
      expect(facade.submitting()).toBe(false);
      expect(getAll).toHaveBeenCalledTimes(1);
    });

    it('ignores a second send while the first is in flight', () => {
      const pending = new Subject<WriteOffCashHeldResponse>();
      writeOff.mockReturnValue(pending);
      facade.startWriteOff(jana);
      facade.setNote('uncollectable');

      facade.submit();
      facade.submit();

      expect(writeOff).toHaveBeenCalledTimes(1);
      expect(facade.canSubmit()).toBe(false);
    });
  });

  it('clears the panel when it is cancelled', () => {
    facade.startWriteOff(jana);
    facade.setNote('typed');

    facade.cancelAction();

    expect(facade.action()).toBeNull();
    expect(facade.actionCopy()).toBeNull();
    expect(facade.amount()).toBe('');
    expect(facade.note()).toBe('');
  });
});
