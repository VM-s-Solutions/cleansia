import {
  MaskedPayoutDetails,
  PayoutDetailsStatus,
  PayoutScheme,
  RevealedPayoutDetails,
} from '@cleansia/admin-services';
import {
  PAYOUT_I18N,
  PayoutRowDeps,
  buildMaskedPayoutRows,
  buildRevealedPayoutRows,
  payoutSchemeLabelKey,
  payoutStatusLabelKey,
} from './employee-payout.models';

const deps: PayoutRowDeps = {
  translate: (key) => key,
  formatDateTime: (value) => (value ? value.toISOString() : '—'),
  resolveCountryName: (countryId) => (countryId === 'cz-id' ? 'Czechia' : ''),
};

const valueOf = (
  rows: readonly { id: string; value: string }[],
  id: string
): string | undefined => rows.find((r) => r.id === id)?.value;

describe('payout enum label keys', () => {
  it('maps every known scheme and status to its own key', () => {
    expect(payoutSchemeLabelKey(PayoutScheme.CzskDomesticWithIban)).toBe(
      `${PAYOUT_I18N}.schemes.czsk_domestic_with_iban`
    );
    expect(payoutSchemeLabelKey(PayoutScheme.SepaIban)).toBe(
      `${PAYOUT_I18N}.schemes.sepa_iban`
    );
    expect(payoutSchemeLabelKey(PayoutScheme.ProviderPayoutToken)).toBe(
      `${PAYOUT_I18N}.schemes.provider_payout_token`
    );
    expect(payoutStatusLabelKey(PayoutDetailsStatus.Provided)).toBe(
      `${PAYOUT_I18N}.statuses.provided`
    );
    expect(
      payoutStatusLabelKey(PayoutDetailsStatus.NeedsReconfirmation)
    ).toBe(`${PAYOUT_I18N}.statuses.needs_reconfirmation`);
  });

  it('falls back to a translated key rather than printing an integer', () => {
    expect(payoutSchemeLabelKey(99 as PayoutScheme)).toBe(
      `${PAYOUT_I18N}.schemes.unknown`
    );
    expect(payoutStatusLabelKey(undefined)).toBe(
      `${PAYOUT_I18N}.statuses.unknown`
    );
  });
});

describe('buildMaskedPayoutRows', () => {
  const masked = MaskedPayoutDetails.fromJS({
    employeeId: 'emp-1',
    scheme: PayoutScheme.CzskDomesticWithIban,
    status: PayoutDetailsStatus.Provided,
    bankCountryId: 'cz-id',
    maskedAccount: '****3003',
    bankName: 'Raiffeisenbank',
    confirmedAt: '2026-07-01T10:00:00Z',
    lastRevealedAt: '2026-07-20T08:30:00Z',
    revealCount: 2,
  });

  it('renders only the masked account — no row carries an unmasked identifier', () => {
    const rows = buildMaskedPayoutRows(masked, deps);

    expect(valueOf(rows, 'masked_account')).toBe('****3003');
    expect(rows.map((r) => r.id)).not.toContain('account_number');
    expect(rows.map((r) => r.id)).not.toContain('iban');
    expect(rows.some((r) => /\d{6,}/.test(r.value))).toBe(false);
  });

  it('resolves the bank country to a name, never its stored id', () => {
    const rows = buildMaskedPayoutRows(masked, deps);

    expect(valueOf(rows, 'bank_country')).toBe('Czechia');
    expect(rows.some((r) => r.value === 'cz-id')).toBe(false);
  });

  it('always shows the reveal audit counters, so a recorded reveal is visible', () => {
    const rows = buildMaskedPayoutRows(masked, deps);

    expect(valueOf(rows, 'reveal_count')).toBe('2');
    expect(valueOf(rows, 'last_revealed_at')).toBe(
      new Date('2026-07-20T08:30:00Z').toISOString()
    );
  });

  it('says "never revealed" instead of leaving the counter row blank', () => {
    const rows = buildMaskedPayoutRows(
      MaskedPayoutDetails.fromJS({
        scheme: PayoutScheme.SepaIban,
        status: PayoutDetailsStatus.NeedsReconfirmation,
        maskedAccount: '****1234',
        revealCount: 0,
      }),
      deps
    );

    expect(valueOf(rows, 'last_revealed_at')).toBe(
      `${PAYOUT_I18N}.never_revealed`
    );
    expect(valueOf(rows, 'reveal_count')).toBe('0');
  });

  it('drops optional rows the server did not send rather than printing blanks', () => {
    const rows = buildMaskedPayoutRows(
      MaskedPayoutDetails.fromJS({
        scheme: PayoutScheme.SepaIban,
        status: PayoutDetailsStatus.Provided,
        maskedAccount: '****1234',
        revealCount: 0,
      }),
      deps
    );

    expect(rows.map((r) => r.id)).not.toContain('bank_name');
    expect(rows.map((r) => r.id)).not.toContain('bank_country');
  });
});

describe('buildRevealedPayoutRows', () => {
  it('lists every identifier the reveal returned', () => {
    const rows = buildRevealedPayoutRows(
      RevealedPayoutDetails.fromJS({
        scheme: PayoutScheme.CzskDomesticWithIban,
        accountPrefix: '000000',
        accountNumber: '5885638003',
        bankCode: '5500',
        iban: 'CZ3155000000005885638003',
        swift: 'RZBCCZPP',
        holderName: 'Jana Nova',
      }),
      deps
    );

    expect(rows.map((r) => r.id)).toEqual([
      'account_prefix',
      'account_number',
      'bank_code',
      'iban',
      'swift',
      'holder_name',
    ]);
    expect(valueOf(rows, 'iban')).toBe('CZ3155000000005885638003');
  });

  it('omits the fields a SEPA record does not carry', () => {
    const rows = buildRevealedPayoutRows(
      RevealedPayoutDetails.fromJS({
        scheme: PayoutScheme.SepaIban,
        iban: 'DE89370400440532013000',
      }),
      deps
    );

    expect(rows.map((r) => r.id)).toEqual(['iban']);
  });
});
