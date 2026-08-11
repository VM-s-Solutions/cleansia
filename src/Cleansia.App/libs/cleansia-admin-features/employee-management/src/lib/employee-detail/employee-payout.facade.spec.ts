import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminPayoutDetailsService,
  MaskedPayoutDetails,
  PayoutDetailsStatus,
  PayoutScheme,
  RevealedPayoutDetails,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { NEVER, of, throwError } from 'rxjs';
import { EmployeePayoutFacade } from './employee-payout.facade';

describe('EmployeePayoutFacade', () => {
  let getForEmployee: jest.Mock;
  let reveal: jest.Mock;
  let getOverview: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let facade: EmployeePayoutFacade;

  const maskedRecord = (overrides: Record<string, unknown> = {}) =>
    MaskedPayoutDetails.fromJS({
      employeeId: 'emp-1',
      scheme: PayoutScheme.CzskDomesticWithIban,
      status: PayoutDetailsStatus.Provided,
      bankCountryId: 'cz-id',
      maskedAccount: '****3003',
      bankName: 'Raiffeisenbank',
      revealCount: 0,
      ...overrides,
    });

  beforeEach(() => {
    getForEmployee = jest.fn().mockReturnValue(of(maskedRecord()));
    reveal = jest.fn();
    getOverview = jest
      .fn()
      .mockReturnValue(
        of([{ id: 'cz-id', name: 'Czechia', isoCode: 'CZE', translations: {} }])
      );
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        EmployeePayoutFacade,
        {
          provide: AdminClient,
          useValue: {
            payoutDetailsClient: { reveal },
            adminCountryClient: { getOverview },
          },
        },
        { provide: AdminPayoutDetailsService, useValue: { getForEmployee } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
      ],
    });

    facade = TestBed.inject(EmployeePayoutFacade);
  });

  describe('the masked read', () => {
    it('loads the masked record and exposes only masked rows', () => {
      facade.load('emp-1');

      expect(getForEmployee).toHaveBeenCalledWith('emp-1');
      expect(facade.maskedDetails()?.maskedAccount).toBe('****3003');
      expect(facade.maskedRows().map((r) => r.id)).toContain('masked_account');
      expect(facade.maskedRows().map((r) => r.id)).not.toContain('iban');
      expect(facade.loading()).toBe(false);
      expect(facade.loadFailed()).toBe(false);
    });

    it('has nothing unmasked until a reveal happens', () => {
      facade.load('emp-1');

      expect(facade.revealedDetails()).toBeNull();
      expect(facade.revealedRows()).toEqual([]);
    });

    it('renders empty — not an error — for a cleaner with no details on file', () => {
      getForEmployee.mockReturnValue(of(null));

      facade.load('emp-1');

      expect(facade.isEmpty()).toBe(true);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.maskedDetails()).toBeNull();
      expect(snackbar.showError).not.toHaveBeenCalled();
    });

    it('keeps a failed read distinct from an empty one', () => {
      getForEmployee.mockReturnValue(throwError(() => new Error('offline')));

      facade.load('emp-1');

      expect(facade.loadFailed()).toBe(true);
      expect(facade.isEmpty()).toBe(false);
      expect(facade.loading()).toBe(false);
    });

    it('reports loading while the read is in flight', () => {
      getForEmployee.mockReturnValue(NEVER);

      facade.load('emp-1');

      expect(facade.loading()).toBe(true);
      expect(facade.isEmpty()).toBe(false);
    });
  });

  describe('the reveal', () => {
    beforeEach(() => {
      facade.load('emp-1');
      getForEmployee.mockClear();
    });

    it('goes through the POST reveal command, never a second read', () => {
      reveal.mockReturnValue(
        of(RevealedPayoutDetails.fromJS({ iban: 'CZ3155000000005885638003' }))
      );

      facade.reveal('emp-1');

      expect(reveal).toHaveBeenCalledWith('emp-1');
      expect(facade.revealedDetails()?.iban).toBe('CZ3155000000005885638003');
      expect(facade.revealedRows().map((r) => r.id)).toEqual(['iban']);
    });

    it('re-reads the masked record so the recorded reveal count updates on screen', () => {
      reveal.mockReturnValue(of(RevealedPayoutDetails.fromJS({ iban: 'CZ31' })));
      getForEmployee.mockReturnValue(of(maskedRecord({ revealCount: 1 })));

      facade.reveal('emp-1');

      expect(getForEmployee).toHaveBeenCalledWith('emp-1');
      expect(facade.maskedDetails()?.revealCount).toBe(1);
    });

    it('reveals nothing when the command is refused', () => {
      reveal.mockReturnValue(throwError(() => new Error('rate limited')));

      facade.reveal('emp-1');

      expect(facade.revealedDetails()).toBeNull();
      expect(facade.revealing()).toBe(false);
    });

    it('hiding drops the unmasked value from state, not just from view', () => {
      reveal.mockReturnValue(of(RevealedPayoutDetails.fromJS({ iban: 'CZ31' })));
      facade.reveal('emp-1');

      facade.hide();

      expect(facade.revealedDetails()).toBeNull();
      expect(facade.revealedRows()).toEqual([]);
    });

    it('discards a previously revealed value when the record is re-read', () => {
      reveal.mockReturnValue(of(RevealedPayoutDetails.fromJS({ iban: 'CZ31' })));
      facade.reveal('emp-1');

      facade.load('emp-1');

      expect(facade.revealedDetails()).toBeNull();
    });
  });
});
