import { FormBuilder } from '@angular/forms';
import {
  MyPayoutDetails,
  UpdateBankDetailsCommand,
} from '@cleansia/partner-services';
import { readFileSync } from 'fs';
import { join } from 'path';
import {
  IBAN_LENGTHS,
  asBankReference,
  asGroupedIban,
  canSubmitBankDetails,
  createBankDetailsForm,
  createUpdateBankDetailsCommand,
  digitsOnly,
  ibanOnlyCountry,
  ibanProblem,
  mapPayoutDetailsToBankForm,
  withoutPayoutPadding,
} from './profile-bank.models';

const IBAN_CALCULATOR = join(
  __dirname,
  '../../../../../../../Cleansia.Core.Domain/Payouts/IbanCalculator.cs'
);

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

    it('groups an IBAN in fours as it is read off a statement', () => {
      expect(asGroupedIban('de89370400440532013000')).toBe('DE89 3704 0044 0532 0130 00');
      expect(asGroupedIban('DE89 3704-0044')).toBe('DE89 3704 0044');
      expect(asGroupedIban('DE89')).toBe('DE89');
      expect(asGroupedIban('')).toBe('');
    });

    it('caps an IBAN at the 34 characters ISO 13616 allows', () => {
      expect(asBankReference(asGroupedIban('MT'.padEnd(40, '1')))).toHaveLength(34);
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

  describe('ibanOnlyCountry', () => {
    it('keeps the three parts for a Czech or Slovak bank', () => {
      expect(ibanOnlyCountry('CZ')).toBeNull();
      expect(ibanOnlyCountry('SK')).toBeNull();
    });

    it('keeps the three parts when the bank country is not known', () => {
      expect(ibanOnlyCountry(undefined)).toBeNull();
      expect(ibanOnlyCountry('')).toBeNull();
    });

    it('pays a bank anywhere else to its IBAN alone', () => {
      expect(ibanOnlyCountry('DE')).toBe('DE');
      expect(ibanOnlyCountry('UA')).toBe('UA');
    });
  });

  /**
   * What the server's IbanCalculator refuses, in the order the Android twin names it: another
   * country first, then the country's length, then the shape and the check digits.
   */
  describe('ibanProblem', () => {
    const invalid = { key: 'api.validation.payout.invalid_iban' };
    const otherCountry = { key: 'api.validation.payout.iban_country_mismatch' };

    it('accepts a valid IBAN from the bank country, however it is spaced or cased', () => {
      expect(ibanProblem('DE89370400440532013000', 'DE')).toBeNull();
      expect(ibanProblem('de89 3704 0044 0532 0130 00', 'DE')).toBeNull();
      expect(ibanProblem('AT611904300234573201', 'AT')).toBeNull();
      expect(ibanProblem('GB82WEST12345698765432', 'GB')).toBeNull();
      expect(ibanProblem('NL91ABNA0417164300', 'NL')).toBeNull();
      expect(ibanProblem('UA213223130000026007233566001', 'UA')).toBeNull();
    });

    it('holds a country the registry table lacks to the generic 15–34 bound only', () => {
      expect(ibanProblem('XK051212012345678906', 'XK')).toBeNull();
    });

    it('names another country\'s IBAN first, whatever else is wrong with it', () => {
      expect(ibanProblem('AT611904300234573201', 'DE')).toEqual(otherCountry);
      expect(ibanProblem('AT6119', 'DE')).toEqual(otherCountry);
    });

    it('names the length the bank country\'s IBANs have', () => {
      // 21 characters with valid check digits; a German IBAN has 22.
      expect(ibanProblem('DE5137040044053201300', 'DE')).toEqual({
        key: 'pages.profile.iban_wrong_length',
        length: 22,
      });
      expect(ibanProblem('DE89', 'DE')).toEqual({ key: 'pages.profile.iban_wrong_length', length: 22 });
    });

    it('refuses a mistyped digit by its check digits', () => {
      expect(ibanProblem('DE89370400440532013001', 'DE')).toEqual(invalid);
    });

    it('refuses text that does not start like an IBAN', () => {
      for (const value of ['4111 1111 1111 1111', '89DE370400440532013000', '', 'D']) {
        expect(ibanProblem(value, 'DE')).toEqual(invalid);
      }
      // Check digits that are not digits, at the right length.
      expect(ibanProblem('DEX9370400440532013000', 'DE')).toEqual(invalid);
    });

    it('carries the server\'s registry lengths, entry for entry', () => {
      const source = readFileSync(IBAN_CALCULATOR, 'utf8');
      const table = /RegistryLengths\s*=\s*new\([^)]*\)\s*\{([\s\S]*?)\};/.exec(source)?.[1];
      if (!table) throw new Error('IbanCalculator.RegistryLengths not found — the parser needs updating');
      const server = Object.fromEntries(
        [...table.matchAll(/\["([A-Z]{2})"\]\s*=\s*(\d+)/g)].map(([, code, length]) => [code, Number(length)])
      );

      expect(Object.keys(server).length).toBeGreaterThan(0);
      expect(IBAN_LENGTHS).toEqual(server);
    });
  });

  describe('canSubmitBankDetails', () => {
    it('needs a bank country plus something that identifies the account', () => {
      expect(canSubmitBankDetails(filledForm)).toBe(true);
    });

    it('needs the account number for a Czech or Slovak bank — the IBAN there is the server\'s to derive', () => {
      expect(
        canSubmitBankDetails({
          ...filledForm,
          accountNumber: '',
          bankCode: '',
          accountPrefix: '',
        })
      ).toBe(false);
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

    it('keeps the Czech rule for a Czech or Slovak bank', () => {
      expect(canSubmitBankDetails({ ...filledForm, iban: '' }, 'CZ')).toBe(true);
      expect(canSubmitBankDetails({ ...filledForm, iban: 'not an iban' }, 'SK')).toBe(true);
    });

    describe('for a bank paid to its IBAN alone', () => {
      const german = { ...filledForm, bankCountryId: 'country-de', iban: 'DE89 3704 0044 0532 0130 00' };

      it('accepts a valid IBAN from the bank country', () => {
        expect(canSubmitBankDetails(german, 'DE')).toBe(true);
      });

      it('refuses without an IBAN, whatever the hidden account parts hold', () => {
        expect(canSubmitBankDetails({ ...german, iban: '' }, 'DE')).toBe(false);
      });

      it('leaves the IBAN\'s own check to the save, which names the problem', () => {
        expect(canSubmitBankDetails({ ...german, iban: 'DE89370400440532013001' }, 'DE')).toBe(true);
      });
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
    /**
     * The stored IBAN is the one the server derived from the stored parts, so sent back with edited
     * parts it was refused as `iban_mismatch`.
     */
    it('sends a Czech or Slovak bank the parts and leaves the IBAN to the server', () => {
      const command = createUpdateBankDetailsCommand('emp-1', filledForm, 'CZ');

      expect(command).toBeInstanceOf(UpdateBankDetailsCommand);
      // Every generated member is optional, so an omission is invisible to the
      // compiler — assert the wire payload field by field.
      expect(command.toJSON()).toEqual({
        employeeId: 'emp-1',
        iban: undefined,
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

    it('treats a bank country it cannot name as Czech or Slovak', () => {
      expect(createUpdateBankDetailsCommand('emp-1', filledForm).iban).toBeUndefined();
      expect(createUpdateBankDetailsCommand('emp-1', filledForm).accountNumber).toBe('2000145399');
    });

    it('sends a bank paid to its IBAN alone the IBAN and its country, and none of the hidden parts', () => {
      const command = createUpdateBankDetailsCommand('emp-1', {
        ...filledForm,
        bankCountryId: 'country-de',
        iban: 'DE89 3704 0044 0532 0130 00',
        swift: 'COBADEFF',
      }, 'DE');

      expect(command.toJSON()).toEqual({
        employeeId: 'emp-1',
        iban: 'DE89370400440532013000',
        bankCountryId: 'country-de',
        accountPrefix: undefined,
        accountNumber: undefined,
        bankCode: undefined,
        swift: 'COBADEFF',
        bankName: 'Ceska sporitelna',
        holderName: 'Jana Novakova',
      });
    });
  });
});
