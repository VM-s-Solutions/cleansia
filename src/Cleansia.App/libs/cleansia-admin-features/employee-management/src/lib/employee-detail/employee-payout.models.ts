import {
  MaskedPayoutDetails,
  PayoutDetailsStatus,
  PayoutScheme,
  RevealedPayoutDetails,
} from '@cleansia/admin-services';

export const PAYOUT_I18N = 'pages.employee_detail.payout_details';

const NOT_SET = '—';

export interface PayoutDetailRow {
  readonly id: string;
  readonly label: string;
  readonly value: string;
}

export interface PayoutRowDeps {
  readonly translate: (key: string) => string;
  readonly formatDateTime: (value: Date | undefined) => string;
  readonly resolveCountryName: (countryId: string | undefined) => string;
}

const SCHEME_LABEL_KEYS: Readonly<Partial<Record<PayoutScheme, string>>> = {
  [PayoutScheme.CzskDomesticWithIban]: `${PAYOUT_I18N}.schemes.czsk_domestic_with_iban`,
  [PayoutScheme.SepaIban]: `${PAYOUT_I18N}.schemes.sepa_iban`,
  [PayoutScheme.ProviderPayoutToken]: `${PAYOUT_I18N}.schemes.provider_payout_token`,
};

const STATUS_LABEL_KEYS: Readonly<Partial<Record<PayoutDetailsStatus, string>>> = {
  [PayoutDetailsStatus.Provided]: `${PAYOUT_I18N}.statuses.provided`,
  [PayoutDetailsStatus.NeedsReconfirmation]: `${PAYOUT_I18N}.statuses.needs_reconfirmation`,
};

export function payoutSchemeLabelKey(scheme: PayoutScheme | undefined): string {
  return (
    (scheme === undefined ? undefined : SCHEME_LABEL_KEYS[scheme]) ??
    `${PAYOUT_I18N}.schemes.unknown`
  );
}

export function payoutStatusLabelKey(
  status: PayoutDetailsStatus | undefined
): string {
  return (
    (status === undefined ? undefined : STATUS_LABEL_KEYS[status]) ??
    `${PAYOUT_I18N}.statuses.unknown`
  );
}

function row(id: string, value: string, deps: PayoutRowDeps): PayoutDetailRow {
  return { id, label: deps.translate(`${PAYOUT_I18N}.${id}`), value };
}

function presentRows(
  fields: readonly { id: string; value: string | undefined }[],
  deps: PayoutRowDeps
): PayoutDetailRow[] {
  return fields
    .filter((field) => !!field.value?.trim())
    .map((field) => row(field.id, field.value as string, deps));
}

export function buildMaskedPayoutRows(
  details: MaskedPayoutDetails,
  deps: PayoutRowDeps
): PayoutDetailRow[] {
  return [
    row('masked_account', details.maskedAccount?.trim() || NOT_SET, deps),
    row('scheme', deps.translate(payoutSchemeLabelKey(details.scheme)), deps),
    row('status', deps.translate(payoutStatusLabelKey(details.status)), deps),
    ...presentRows(
      [
        { id: 'bank_name', value: details.bankName },
        {
          id: 'bank_country',
          value: deps.resolveCountryName(details.bankCountryId),
        },
      ],
      deps
    ),
    row('confirmed_at', deps.formatDateTime(details.confirmedAt), deps),
    row(
      'last_revealed_at',
      details.lastRevealedAt
        ? deps.formatDateTime(details.lastRevealedAt)
        : deps.translate(`${PAYOUT_I18N}.never_revealed`),
      deps
    ),
    row('reveal_count', String(details.revealCount ?? 0), deps),
  ];
}

export function buildRevealedPayoutRows(
  details: RevealedPayoutDetails,
  deps: PayoutRowDeps
): PayoutDetailRow[] {
  return presentRows(
    [
      { id: 'account_prefix', value: details.accountPrefix },
      { id: 'account_number', value: details.accountNumber },
      { id: 'bank_code', value: details.bankCode },
      { id: 'iban', value: details.iban },
      { id: 'swift', value: details.swift },
      { id: 'holder_name', value: details.holderName },
    ],
    deps
  );
}
