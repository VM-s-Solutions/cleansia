import { FormBuilder } from '@angular/forms';
import {
  MyPayoutDetails,
  UpdateBankDetailsCommand,
} from '@cleansia/partner-services';
import {
  asBankReference,
  canSubmitBankDetails,
  createBankDetailsForm,
  createUpdateBankDetailsCommand,
  digitsOnly,
  mapPayoutDetailsToBankForm,
  withoutPayoutPadding,
} from './profile-bank.models';

describe('profile bank models', () => {
  const fb = new FormBuilder().nonNullable;

  const filledForm = {
    bankCountryId: 'country-cz',
    accountPrefix: '19',
    accountNumber: '2000145399',
    bankCode: '0800',
    iban: 'CZ6508000000192000145399',
    swift: 'GIBACZPX',
    bankName: 'Ceska sporitelna',
    holderName: 'Jana Novakova',
  };

  describe('input normalization', () => {
    it('keeps only digits in the account fields', () => {
      expect(digitsOnly('19-2000145399/0800')).toBe('1920001453990800');
      expect(digitsOnly(' 5500 ')).toBe('5500');
    });

    it('does not cap the length — a too-long value is the server to reject, not us to truncate', () => {
      expect(digitsOnly('4111111111111111')).toBe('4111111111111111');
    });

    it('uppercases an IBAN or SWIFT and drops its separators', () => {
      expect(asBankReference('cz65 0800 0000 1920 0014 5399')).toBe(
        'CZ6508000000192000145399'
      );
      expect(asBankReference('giba-cz-px')).toBe('GIBACZPX');
    });

    it('strips the stored zero padding for display', () => {
      expect(withoutPayoutPadding('0005885638003')).toBe('5885638003');
      expect(withoutPayoutPadding('000000')).toBe('');
      expect(withoutPayoutPadding(undefined)).toBe('');
    });
  });

  describe('createBankDetailsForm', () => {
    it('carries the eight capture fields and starts empty', () => {
      const form = createBankDetailsForm(fb);

      expect(Object.keys(form.controls)).toEqual([
        'bankCountryId',
        'accountPrefix',
        'accountNumber',
        'bankCode',
        'iban',
        'swift',
        'bankName',
        'holderName',
      ]);
      expect(form.getRawValue()).toEqual({
        bankCountryId: '',
        accountPrefix: '',
        accountNumber: '',
        bankCode: '',
        iban: '',
        swift: '',
        bankName: '',
        holderName: '',
      });
    });

    it('holds no validator of its own — the server owns every payout rule', () => {
      const form = createBankDetailsForm(fb);

      expect(form.valid).toBe(true);
      for (const control of Object.values(form.controls)) {
        expect(control.validator).toBeNull();
      }
    });
  });

  describe('canSubmitBankDetails', () => {
    it('needs a bank country plus something that identifies the account', () => {
      expect(canSubmitBankDetails(filledForm)).toBe(true);
    });

    it('accepts an IBAN on its own', () => {
      expect(
        canSubmitBankDetails({
          ...filledForm,
          accountNumber: '',
          bankCode: '',
          accountPrefix: '',
        })
      ).toBe(true);
    });

    it('accepts an account number without a prefix — the prefix is optional', () => {
      expect(
        canSubmitBankDetails({ ...filledForm, accountPrefix: '', iban: '' })
      ).toBe(true);
    });

    it('refuses when the bank country is missing', () => {
      expect(canSubmitBankDetails({ ...filledForm, bankCountryId: '' })).toBe(
        false
      );
    });

    it('refuses when nothing identifies the account', () => {
      expect(
        canSubmitBankDetails({ ...filledForm, accountNumber: '', iban: '' })
      ).toBe(false);
    });
  });

  describe('mapPayoutDetailsToBankForm', () => {
    it('unpads the stored account parts and keeps the rest as sent', () => {
      const details = MyPayoutDetails.fromJS({
        bankCountryId: 'country-cz',
        accountPrefix: '000019',
        accountNumber: '0002000145399',
        bankCode: '0800',
        iban: 'CZ6508000000192000145399',
        swift: 'GIBACZPX',
        bankName: 'Ceska sporitelna',
        holderName: 'Jana Novakova',
      });

      expect(mapPayoutDetailsToBankForm(details, 'country-sk')).toEqual({
        bankCountryId: 'country-cz',
        accountPrefix: '19',
        accountNumber: '2000145399',
        bankCode: '0800',
        iban: 'CZ6508000000192000145399',
        swift: 'GIBACZPX',
        bankName: 'Ceska sporitelna',
        holderName: 'Jana Novakova',
      });
    });

    it('opens empty for a cleaner with no details, pre-filling only the bank country', () => {
      expect(mapPayoutDetailsToBankForm(null, 'country-cz')).toEqual({
        bankCountryId: 'country-cz',
        accountPrefix: '',
        accountNumber: '',
        bankCode: '',
        iban: '',
        swift: '',
        bankName: '',
        holderName: '',
      });
    });

    it('leaves the bank country empty when the profile has no country either', () => {
      expect(mapPayoutDetailsToBankForm(null, undefined).bankCountryId).toBe('');
    });
  });

  describe('createUpdateBankDetailsCommand', () => {
    it('carries all nine fields the endpoint accepts', () => {
      const command = createUpdateBankDetailsCommand('emp-1', filledForm);

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

    it('sends an untouched optional field as undefined rather than an empty string', () => {
      const command = createUpdateBankDetailsCommand('emp-1', {
        ...filledForm,
        accountPrefix: '',
        swift: '  ',
        bankName: '',
        holderName: '',
        iban: '',
      });

      expect(command.accountPrefix).toBeUndefined();
      expect(command.swift).toBeUndefined();
      expect(command.bankName).toBeUndefined();
      expect(command.holderName).toBeUndefined();
      expect(command.iban).toBeUndefined();
      expect(command.accountNumber).toBe('2000145399');
    });
  });
});
