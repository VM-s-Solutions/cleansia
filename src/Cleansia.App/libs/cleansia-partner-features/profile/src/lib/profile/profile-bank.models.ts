import { FormControl, FormGroup, NonNullableFormBuilder } from '@angular/forms';
import {
  MyPayoutDetails,
  UpdateBankDetailsCommand,
} from '@cleansia/partner-services';

export interface BankDetailsFormValue {
  bankCountryId: string;
  accountPrefix: string;
  accountNumber: string;
  bankCode: string;
  iban: string;
  swift: string;
  bankName: string;
  holderName: string;
}

export type BankDetailsForm = FormGroup<{
  bankCountryId: FormControl<string>;
  accountPrefix: FormControl<string>;
  accountNumber: FormControl<string>;
  bankCode: FormControl<string>;
  iban: FormControl<string>;
  swift: FormControl<string>;
  bankName: FormControl<string>;
  holderName: FormControl<string>;
}>;

export function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

export function asBankReference(value: string): string {
  return value.toUpperCase().replace(/[^A-Z0-9]/g, '');
}

/** `de89370400440532013000` → `DE89 3704 0044 0532 0130 00`: an IBAN is read and compared in fours. */
export function asGroupedIban(value: string): string {
  return asBankReference(value).replace(/(.{4})(?=.)/g, '$1 ');
}

/**
 * The bank countries whose accounts are written `prefix-number/bank code`, the parts the server derives
 * the IBAN from (ADR-0034 D5.2). A bank anywhere else is paid to its IBAN alone (owner ruling
 * 2026-10-02). The server reads the scheme from `CountryConfiguration.PayoutScheme`, which no endpoint
 * serves, so the client names the two countries that scheme covers.
 */
const ACCOUNT_PARTS_COUNTRIES: readonly string[] = ['CZ', 'SK'];

/**
 * The country an IBAN alone pays into, as its alpha-2 code — null for a Czech or Slovak bank, and null
 * when the country is not known, which keeps the form as it was: the three parts plus an optional IBAN.
 */
export function ibanOnlyCountry(bankCountryAlpha2: string | undefined): string | null {
  return bankCountryAlpha2 && !ACCOUNT_PARTS_COUNTRIES.includes(bankCountryAlpha2)
    ? bankCountryAlpha2
    : null;
}

/** ISO 13616 registry lengths — `IbanCalculator.RegistryLengths` on the server, entry for entry. */
export const IBAN_LENGTHS: Readonly<Record<string, number>> = {
  AT: 20, BE: 16, BG: 22, CH: 21, CY: 28, CZ: 24, DE: 22, DK: 18, EE: 20, ES: 24, FI: 18,
  FR: 27, GB: 22, GR: 27, HR: 21, HU: 28, IE: 22, IT: 27, LT: 20, LU: 20, LV: 21, MT: 31,
  NL: 18, NO: 15, PL: 28, PT: 25, RO: 24, SE: 24, SI: 19, SK: 24, UA: 29,
};

/**
 * Why the server would refuse this IBAN for a bank in `bankCountryAlpha2`, as the key it answers with
 * (it reaches the cleaner under `api.`), or null. The checks and their order are
 * `PayoutDetailsValidator.ValidateSepa`'s: the IBAN's own shape, its registry length and its ISO 7064
 * mod-97 check digits first, then its country against the bank's. The server still decides; this only
 * says so before the cleaner presses Save.
 */
export function ibanProblem(
  iban: string,
  bankCountryAlpha2: string
): 'validation.payout.invalid_iban' | 'validation.payout.iban_country_mismatch' | null {
  const value = asBankReference(iban);
  if (!isValidIban(value)) return 'validation.payout.invalid_iban';
  return value.startsWith(bankCountryAlpha2) ? null : 'validation.payout.iban_country_mismatch';
}

/** `IbanCalculator.IsValid`: 15–34 characters, two letters and two digits, the registry length, mod 97 = 1. */
function isValidIban(value: string): boolean {
  if (!/^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$/.test(value)) return false;
  const expected = IBAN_LENGTHS[value.slice(0, 2)];
  if (expected !== undefined && value.length !== expected) return false;

  let remainder = 0;
  for (const character of value.slice(4) + value.slice(0, 4)) {
    remainder = /\d/.test(character)
      ? (remainder * 10 + Number(character)) % 97
      : (remainder * 100 + character.charCodeAt(0) - 55) % 97;
  }
  return remainder === 1;
}

/** The server stores the local parts zero-padded to fixed widths; re-padding them is its job. */
export function withoutPayoutPadding(value: string | undefined): string {
  return (value ?? '').trim().replace(/^0+/, '');
}

export type NormalizedBankField = Extract<
  keyof BankDetailsFormValue,
  'accountPrefix' | 'accountNumber' | 'bankCode' | 'iban' | 'swift'
>;

export const BANK_FIELD_NORMALIZERS: Readonly<
  Record<NormalizedBankField, (value: string) => string>
> = {
  accountPrefix: digitsOnly,
  accountNumber: digitsOnly,
  bankCode: digitsOnly,
  iban: asGroupedIban,
  swift: asBankReference,
};

/**
 * The payout destination is captured as the parts a Czech or Slovak cleaner reads off their
 * statement, because the server derives the IBAN from them, and as the IBAN alone for a bank
 * anywhere else. It carries no validators: the account checksum, the bank code, whether a
 * supplied IBAN agrees and whether a card number was typed in are all the server's to answer.
 * The one check made here is the IBAN-only one (`ibanProblem`), which the facade shows next to
 * the field because it depends on the bank country the form holds.
 */
export function createBankDetailsForm(
  fb: NonNullableFormBuilder
): BankDetailsForm {
  return fb.group({
    bankCountryId: '',
    accountPrefix: '',
    accountNumber: '',
    bankCode: '',
    iban: '',
    swift: '',
    bankName: '',
    holderName: '',
  });
}

/**
 * The bank's country plus something that identifies the account — the rest is the server's call. A
 * bank paid to its IBAN alone needs an IBAN the server would take.
 */
export function canSubmitBankDetails(
  value: BankDetailsFormValue,
  bankCountryAlpha2?: string
): boolean {
  if (!value.bankCountryId.trim()) return false;

  const ibanCountry = ibanOnlyCountry(bankCountryAlpha2);
  if (ibanCountry) return ibanProblem(value.iban, ibanCountry) === null;

  return !!value.accountNumber.trim() || !!value.iban.trim();
}

export function mapPayoutDetailsToBankForm(
  details: MyPayoutDetails | null,
  fallbackCountryId: string | undefined
): BankDetailsFormValue {
  return {
    bankCountryId: details?.bankCountryId?.trim() || fallbackCountryId || '',
    accountPrefix: withoutPayoutPadding(details?.accountPrefix),
    accountNumber: withoutPayoutPadding(details?.accountNumber),
    bankCode: details?.bankCode?.trim() ?? '',
    iban: details?.iban?.trim() ?? '',
    swift: details?.swift?.trim() ?? '',
    bankName: details?.bankName?.trim() ?? '',
    holderName: details?.holderName?.trim() ?? '',
  };
}

function blankToUndefined(value: string): string | undefined {
  return value.trim() || undefined;
}

/**
 * A bank paid to its IBAN alone sends the IBAN with its country and no account parts: the parts are
 * not on screen then, and whatever a cleaner typed there before changing the country is not theirs to
 * send. The IBAN goes as the server stores it, without the grouping spaces.
 */
export function createUpdateBankDetailsCommand(
  employeeId: string,
  value: BankDetailsFormValue,
  bankCountryAlpha2?: string
): UpdateBankDetailsCommand {
  const ibanOnly = ibanOnlyCountry(bankCountryAlpha2) !== null;
  const command = new UpdateBankDetailsCommand();
  command.employeeId = employeeId;
  command.iban = blankToUndefined(asBankReference(value.iban));
  command.bankCountryId = blankToUndefined(value.bankCountryId);
  command.accountPrefix = ibanOnly ? undefined : blankToUndefined(value.accountPrefix);
  command.accountNumber = ibanOnly ? undefined : blankToUndefined(value.accountNumber);
  command.bankCode = ibanOnly ? undefined : blankToUndefined(value.bankCode);
  command.swift = blankToUndefined(value.swift);
  command.bankName = blankToUndefined(value.bankName);
  command.holderName = blankToUndefined(value.holderName);

  return command;
}
