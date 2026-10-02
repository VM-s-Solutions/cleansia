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

/** The longest IBAN ISO 13616 allows, and the field's cap. */
const IBAN_MAX_LENGTH = 34;

/** `de89370400440532013000` → `DE89 3704 0044 0532 0130 00`: an IBAN is read and compared in fours. */
export function asGroupedIban(value: string): string {
  return asBankReference(value)
    .slice(0, IBAN_MAX_LENGTH)
    .replace(/(.{4})(?=.)/g, '$1 ');
}

/**
 * The bank countries whose accounts are entered as `prefix-number/bank code`: the server's
 * CzskDomesticWithIban scheme, which derives the IBAN from those parts (ADR-0034 D5.2). A bank in any
 * other country is entered as one IBAN (owner ruling 2026-10-02). The server reads the scheme from
 * `CountryConfiguration.PayoutScheme`, which no endpoint serves, so the client names its two countries.
 */
const DOMESTIC_ACCOUNT_COUNTRIES: readonly string[] = ['CZ', 'SK'];

/**
 * The country a bank entered as one IBAN is in, as the code its IBANs start with. Null for a Czech or
 * Slovak bank, and for a country the list cannot name (none picked yet, or the list failed to load):
 * both are entered as the three parts.
 */
export function ibanOnlyCountry(bankCountryAlpha2: string | undefined): string | null {
  return bankCountryAlpha2 && !DOMESTIC_ACCOUNT_COUNTRIES.includes(bankCountryAlpha2)
    ? bankCountryAlpha2
    : null;
}

/** ISO 13616 registry lengths — `IbanCalculator.RegistryLengths` on the server, entry for entry. */
export const IBAN_LENGTHS: Readonly<Record<string, number>> = {
  AT: 20, BE: 16, BG: 22, CH: 21, CY: 28, CZ: 24, DE: 22, DK: 18, EE: 20, ES: 24, FI: 18,
  FR: 27, GB: 22, GR: 27, HR: 21, HU: 28, IE: 22, IT: 27, LT: 20, LU: 20, LV: 21, MT: 31,
  NL: 18, NO: 15, PL: 28, PT: 25, RO: 24, SE: 24, SI: 19, SK: 24, UA: 29,
};

const INVALID_IBAN = 'api.validation.payout.invalid_iban';

/**
 * What the server's IbanCalculator would refuse about this IBAN for a bank in `bankCountryAlpha2`, as
 * the translation key to show (with the length its message names), or null when it would take it.
 * In the order a cleaner can act on it, as the Android twin checks it: another country's IBAN first,
 * then a length that is not the country's, then the shape (15–34, two letters and two digits) and the
 * ISO 7064 mod-97 check digits. The server's own messages, except the length: the server folds that
 * into `invalid_iban`, so its message is the client's and names the length.
 */
export function ibanProblem(
  iban: string,
  bankCountryAlpha2: string
): { key: string; length?: number } | null {
  const value = asBankReference(iban);
  if (!/^[A-Z]{2}/.test(value)) return { key: INVALID_IBAN };
  if (!value.startsWith(bankCountryAlpha2)) {
    return { key: 'api.validation.payout.iban_country_mismatch' };
  }

  const length = IBAN_LENGTHS[value.slice(0, 2)];
  if (length !== undefined && value.length !== length) {
    return { key: 'pages.profile.iban_wrong_length', length };
  }

  const shaped = /^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$/.test(value);
  return shaped && mod97(value.slice(4) + value.slice(0, 4)) === 1 ? null : { key: INVALID_IBAN };
}

/** ISO 7064 MOD 97-10 over an IBAN rearranged to `BBAN + country + check digits`, A = 10 … Z = 35. */
function mod97(value: string): number {
  let remainder = 0;
  for (const character of value) {
    remainder = /\d/.test(character)
      ? (remainder * 10 + Number(character)) % 97
      : (remainder * 100 + character.charCodeAt(0) - 55) % 97;
  }
  return remainder;
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
 * The payout destination. A Czech or Slovak cleaner enters the parts they read off their statement,
 * and the server derives the IBAN, so everything about those parts (the account checksum, the bank
 * code, a card number typed in) is the server's to answer. A bank anywhere else is one IBAN, checked
 * by the facade with `ibanProblem` before the round trip — not by a validator here, because the check
 * depends on the bank country the form holds. The server checks it again.
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

/** The bank's country plus the account in the form that country takes — the rest is the server's call. */
export function canSubmitBankDetails(
  value: BankDetailsFormValue,
  bankCountryAlpha2?: string
): boolean {
  if (!value.bankCountryId.trim()) return false;

  return ibanOnlyCountry(bankCountryAlpha2)
    ? !!value.iban.trim()
    : !!value.accountNumber.trim();
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
 * Each scheme sends only its own identifier. A Czech or Slovak IBAN is the server's to derive, and
 * the stored one sent back with edited parts was refused as `iban_mismatch`; an IBAN account has no
 * parts, and whatever was typed there before the country changed is not on screen to send. The IBAN
 * goes as the server stores it, without the grouping spaces.
 */
export function createUpdateBankDetailsCommand(
  employeeId: string,
  value: BankDetailsFormValue,
  bankCountryAlpha2?: string
): UpdateBankDetailsCommand {
  const domestic = ibanOnlyCountry(bankCountryAlpha2) === null;
  const command = new UpdateBankDetailsCommand();
  command.employeeId = employeeId;
  command.iban = domestic ? undefined : blankToUndefined(asBankReference(value.iban));
  command.bankCountryId = blankToUndefined(value.bankCountryId);
  command.accountPrefix = domestic ? blankToUndefined(value.accountPrefix) : undefined;
  command.accountNumber = domestic ? blankToUndefined(value.accountNumber) : undefined;
  command.bankCode = domestic ? blankToUndefined(value.bankCode) : undefined;
  command.swift = blankToUndefined(value.swift);
  command.bankName = blankToUndefined(value.bankName);
  command.holderName = blankToUndefined(value.holderName);

  return command;
}
