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
  iban: asBankReference,
  swift: asBankReference,
};

/**
 * The payout destination is captured as the parts a Czech or Slovak cleaner reads off their
 * statement, because the server derives the IBAN from them. It carries no validators: the
 * account checksum, the bank code, whether a supplied IBAN agrees and whether a card number
 * was typed in are all the server's to answer, and a second copy of those rules here would
 * gate a cleaner's income on a client that disagrees.
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

/** The bank's country plus something that identifies the account — the rest is the server's call. */
export function canSubmitBankDetails(value: BankDetailsFormValue): boolean {
  return (
    !!value.bankCountryId.trim() &&
    (!!value.accountNumber.trim() || !!value.iban.trim())
  );
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

export function createUpdateBankDetailsCommand(
  employeeId: string,
  value: BankDetailsFormValue
): UpdateBankDetailsCommand {
  const command = new UpdateBankDetailsCommand();
  command.employeeId = employeeId;
  command.iban = blankToUndefined(value.iban);
  command.bankCountryId = blankToUndefined(value.bankCountryId);
  command.accountPrefix = blankToUndefined(value.accountPrefix);
  command.accountNumber = blankToUndefined(value.accountNumber);
  command.bankCode = blankToUndefined(value.bankCode);
  command.swift = blankToUndefined(value.swift);
  command.bankName = blankToUndefined(value.bankName);
  command.holderName = blankToUndefined(value.holderName);

  return command;
}
