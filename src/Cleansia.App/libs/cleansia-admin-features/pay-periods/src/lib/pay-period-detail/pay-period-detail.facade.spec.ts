import { TestBed } from '@angular/core/testing';
import { AdminClient, ClosePayPeriodCommand } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PayPeriodDetailFacade } from './pay-period-detail.facade';

describe('PayPeriodDetailFacade', () => {
  let facade: PayPeriodDetailFacade;
  let detailsMock: jest.Mock;
  let closeMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    detailsMock = jest.fn().mockReturnValue(of({ id: 'period-1' }));
    closeMock = jest.fn().mockReturnValue(of({ payPeriodId: 'period-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        PayPeriodDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPayPeriodClient: { details: detailsMock, close: closeMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
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

  it('re-reads the period after a close lands, and not when it fails', () => {
    facade.closePayPeriod('period-1', 'done');
    expect(detailsMock).toHaveBeenCalledTimes(1);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pay_periods.messages.close_success'
    );

    closeMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.closePayPeriod('period-1', 'done');
    expect(detailsMock).toHaveBeenCalledTimes(1);
  });

  it('renders an absent date as a dash', () => {
    expect(facade.formatDate(null)).toBe('-');
    expect(facade.formatDateTime(undefined)).toBe('-');
  });

  it('derives the status badge class, falling back when the status is absent', () => {
    expect(facade.getStatusClass('Closed')).toBe('status-badge status-closed');
    expect(facade.getStatusClass(null)).toBe('status-badge status-unknown');
  });

  describe('command bodies on the wire', () => {
    it('serializes a close with the period id and the notes', () => {
      facade.closePayPeriod('period-1', 'all invoices generated');

      const command: ClosePayPeriodCommand = closeMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(ClosePayPeriodCommand);
      expect(command.toJSON()).toEqual({
        payPeriodId: 'period-1',
        notes: 'all invoices generated',
      });
    });

    it('leaves the notes undefined when the caller omits them', () => {
      facade.closePayPeriod('period-1');

      const command: ClosePayPeriodCommand = closeMock.mock.calls[0][0];
      expect(command.toJSON()).toEqual({
        payPeriodId: 'period-1',
        notes: undefined,
      });
    });
  });
});
