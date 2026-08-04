import { TestBed } from '@angular/core/testing';
import {
  CountryListItem,
  EmployeeItem,
  MyPayoutDetails,
  PartnerClient,
  PartnerPayoutDetailsService,
  UpdateBankDetailsCommand,
  UpdateBankDetailsResponse,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ProfileBankFacade } from './profile-bank.facade';

describe('ProfileBankFacade', () => {
  let employeeClient: {
    getCurrentEmployee: jest.Mock;
    updateBankDetails: jest.Mock;
  };
  let countryClient: { getOverview: jest.Mock };
  let payoutDetails: { getMine: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showError: jest.Mock;
    showApiError: jest.Mock;
  };
  let dispatch: jest.Mock;

  const employee = EmployeeItem.fromJS({ id: 'emp-1', countryId: 'country-cz' });

  const countries = [
    CountryListItem.fromJS({ id: 'country-cz', name: 'Czechia', isoCode: 'CZ' }),
    CountryListItem.fromJS({ id: 'country-sk', name: 'Slovakia', isoCode: 'SK' }),
  ];

  const savedDetails = MyPayoutDetails.fromJS({
    bankCountryId: 'country-sk',
    accountPrefix: '000019',
    accountNumber: '0002000145399',
    bankCode: '0800',
    iban: 'CZ6508000000192000145399',
    swift: 'GIBACZPX',
    bankName: 'Ceska sporitelna',
    holderName: 'Jana Novakova',
  });

  const createFacade = (): ProfileBankFacade => {
    TestBed.configureTestingModule({
      providers: [
        ProfileBankFacade,
        {
          provide: PartnerClient,
          useValue: { employeeClient, countryClient: countryClient },
        },
        { provide: PartnerPayoutDetailsService, useValue: payoutDetails },
        { provide: SnackbarService, useValue: snackbar },
        { provide: Store, useValue: { dispatch } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
      ],
    });

    return TestBed.inject(ProfileBankFacade);
  };

  beforeEach(() => {
    employeeClient = {
      getCurrentEmployee: jest.fn().mockReturnValue(of(employee)),
      updateBankDetails: jest
        .fn()
        .mockReturnValue(
          of(UpdateBankDetailsResponse.fromJS({ employeeId: 'emp-1' }))
        ),
    };
    countryClient = { getOverview: jest.fn().mockReturnValue(of(countries)) };
    payoutDetails = { getMine: jest.fn().mockReturnValue(of(null)) };
    snackbar = {
      showSuccess: jest.fn(),
      showError: jest.fn(),
      showApiError: jest.fn(),
    };
    dispatch = jest.fn();
  });

  describe('load', () => {
    it('starts in the loading state and clears it once the data arrives', () => {
      const facade = createFacade();
      expect(facade.loading()).toBe(false);

      facade.load();

      expect(facade.loading()).toBe(false);
      expect(facade.loadFailed()).toBe(false);
    });

    it('renders an empty form — not an error — for a cleaner with no details yet', () => {
      const facade = createFacade();

      facade.load();

      expect(facade.loadFailed()).toBe(false);
      expect(facade.formGroup.getRawValue()).toEqual({
        // The bank is usually in the country the cleaner lives in, so pre-fill it.
        bankCountryId: 'country-cz',
        accountPrefix: '',
        accountNumber: '',
        bankCode: '',
        iban: '',
        swift: '',
        bankName: '',
        holderName: '',
      });
      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(snackbar.showApiError).not.toHaveBeenCalled();
    });

    it('fills the form from saved details, unpadded, and keeps their bank country', () => {
      payoutDetails.getMine.mockReturnValue(of(savedDetails));
      const facade = createFacade();

      facade.load();

      expect(facade.formGroup.getRawValue()).toEqual({
        bankCountryId: 'country-sk',
        accountPrefix: '19',
        accountNumber: '2000145399',
        bankCode: '0800',
        iban: 'CZ6508000000192000145399',
        swift: 'GIBACZPX',
        bankName: 'Ceska sporitelna',
        holderName: 'Jana Novakova',
      });
      expect(facade.canSubmit()).toBe(true);
    });

    it('offers every country, not only the serviced ones — a cross-border payout is supported', () => {
      const facade = createFacade();

      facade.load();

      expect(countryClient.getOverview).toHaveBeenCalled();
      expect(facade.countries()).toEqual([
        { label: 'Czechia (CZ)', value: 'country-cz' },
        { label: 'Slovakia (SK)', value: 'country-sk' },
      ]);
    });

    it('renders the error state when the payout read really fails', () => {
      payoutDetails.getMine.mockReturnValue(
        throwError(() => ({ errors: { x: 'employee.not_found' } }))
      );
      const facade = createFacade();

      facade.load();

      expect(facade.loadFailed()).toBe(true);
      expect(facade.loading()).toBe(false);
    });

    it('renders the error state when the employee read fails', () => {
      employeeClient.getCurrentEmployee.mockReturnValue(
        throwError(() => new Error('offline'))
      );
      const facade = createFacade();

      facade.load();

      expect(facade.loadFailed()).toBe(true);
    });

    it('retries a failed load', () => {
      payoutDetails.getMine.mockReturnValueOnce(
        throwError(() => new Error('offline'))
      );
      const facade = createFacade();
      facade.load();
      expect(facade.loadFailed()).toBe(true);

      facade.retry();

      expect(facade.loadFailed()).toBe(false);
      expect(payoutDetails.getMine).toHaveBeenCalledTimes(2);
    });
  });

  describe('input normalization', () => {
    it('keeps only digits in the account fields as the cleaner types', () => {
      const facade = createFacade();
      facade.load();

      facade.formGroup.controls.accountNumber.setValue('19-2000145399/0800');

      expect(facade.formGroup.controls.accountNumber.value).toBe(
        '1920001453990800'
      );
    });

    it('uppercases the IBAN and drops its spacing', () => {
      const facade = createFacade();
      facade.load();

      facade.formGroup.controls.iban.setValue('cz65 0800 0000 1920 0014 5399');

      expect(facade.formGroup.controls.iban.value).toBe(
        'CZ6508000000192000145399'
      );
    });

    it('leaves the free-text fields alone', () => {
      const facade = createFacade();
      facade.load();

      facade.formGroup.controls.holderName.setValue('Jana Nováková');

      expect(facade.formGroup.controls.holderName.value).toBe('Jana Nováková');
    });
  });

  describe('canSubmit', () => {
    it('stays false until a bank country and an account identifier are present', () => {
      const facade = createFacade();
      facade.load();
      expect(facade.canSubmit()).toBe(false);

      facade.formGroup.controls.accountNumber.setValue('2000145399');

      expect(facade.canSubmit()).toBe(true);
    });

    it('accepts an IBAN on its own', () => {
      const facade = createFacade();
      facade.load();

      facade.formGroup.controls.iban.setValue('CZ6508000000192000145399');

      expect(facade.canSubmit()).toBe(true);
    });
  });

  describe('save', () => {
    const fill = (facade: ProfileBankFacade): void => {
      facade.formGroup.setValue({
        bankCountryId: 'country-cz',
        accountPrefix: '19',
        accountNumber: '2000145399',
        bankCode: '0800',
        iban: 'CZ6508000000192000145399',
        swift: 'GIBACZPX',
        bankName: 'Ceska sporitelna',
        holderName: 'Jana Novakova',
      });
    };

    it('sends a command carrying all nine fields', () => {
      const facade = createFacade();
      facade.load();
      fill(facade);

      facade.onSubmit();

      const command = employeeClient.updateBankDetails.mock
        .calls[0][0] as UpdateBankDetailsCommand;
      expect(command).toBeInstanceOf(UpdateBankDetailsCommand);
      // Every generated member is optional, so an omission is invisible to the
      // compiler — assert the wire payload field by field.
      expect(command.toJSON()).toEqual({
        employeeId: 'emp-1',
        iban: 'CZ6508000000192000145399',
        bankCountryId: 'country-cz',
        accountPrefix: '19',
        accountNumber: '2000145399',
        bankCode: '0800',
        swift: 'GIBACZPX',
        bankName: 'Ceska sporitelna',
        holderName: 'Jana Novakova',
      });
    });

    it('confirms the save, clears the saving flag and refreshes the completeness gate', () => {
      const facade = createFacade();
      facade.load();
      fill(facade);

      facade.onSubmit();

      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'global.messages.profile.bank_details_saved'
      );
      expect(facade.saving()).toBe(false);
      expect(dispatch).toHaveBeenCalled();
    });

    it('does not confirm a refused save, and clears the saving flag', () => {
      // The refusal reaches the cleaner as the shared interceptor's
      // api.validation.payout.* toast; a second one from here would double up.
      employeeClient.updateBankDetails.mockReturnValue(
        throwError(() => ({
          errors: { AccountNumber: 'validation.payout.invalid_account_number' },
        }))
      );
      const facade = createFacade();
      facade.load();
      fill(facade);

      facade.onSubmit();

      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(snackbar.showApiError).not.toHaveBeenCalled();
      expect(facade.saving()).toBe(false);
    });

    it('does not call the endpoint when nothing identifies the account', () => {
      const facade = createFacade();
      facade.load();

      facade.onSubmit();

      expect(employeeClient.updateBankDetails).not.toHaveBeenCalled();
    });

    it('reports the profile is not loaded rather than sending an empty employee id', () => {
      employeeClient.getCurrentEmployee.mockReturnValue(
        of(EmployeeItem.fromJS({ countryId: 'country-cz' }))
      );
      const facade = createFacade();
      facade.load();
      fill(facade);

      facade.onSubmit();

      expect(employeeClient.updateBankDetails).not.toHaveBeenCalled();
      expect(snackbar.showError).toHaveBeenCalledWith(
        'global.messages.profile.not_loaded'
      );
    });
  });
});
