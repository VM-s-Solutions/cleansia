import { Injectable, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CountryListItem,
  PartnerClient,
  PartnerPayoutDetailsService,
} from '@cleansia/partner-services';
import { checkEmployeeCurrent } from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { catchError, combineLatest, finalize, of, takeUntil } from 'rxjs';
import {
  BANK_FIELD_NORMALIZERS,
  BankDetailsFormValue,
  NormalizedBankField,
  canSubmitBankDetails,
  createBankDetailsForm,
  createUpdateBankDetailsCommand,
  ibanOnlyCountry,
  ibanProblem,
  mapPayoutDetailsToBankForm,
} from './profile-bank.models';

const NORMALIZED_BANK_FIELDS = Object.keys(
  BANK_FIELD_NORMALIZERS
) as NormalizedBankField[];

@Injectable()
export class ProfileBankFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly payoutDetailsService = inject(PartnerPayoutDetailsService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly store = inject(Store);

  readonly formGroup = createBankDetailsForm(inject(FormBuilder).nonNullable);

  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);
  readonly countries = signal<ICleansiaSelectOption[]>([]);

  private readonly employeeId = signal('');
  private readonly formValue = signal<BankDetailsFormValue>(
    this.formGroup.getRawValue()
  );
  /** Country id → ISO alpha-2, the code an IBAN starts with. */
  private readonly countryAlpha2 = signal<ReadonlyMap<string, string>>(new Map());
  private readonly bankCountryAlpha2 = computed(() =>
    this.countryAlpha2().get(this.formValue().bankCountryId)
  );

  /** The country a bank paid to its IBAN alone is in; null keeps the Czech and Slovak three parts. */
  readonly ibanCountry = computed(() => ibanOnlyCountry(this.bankCountryAlpha2()));

  /**
   * The server's key for refusing the IBAN as it stands, for a bank paid to its IBAN alone. Read here,
   * not from a validator on the control: it changes with the bank country as well as with the IBAN,
   * and the text input redraws its own errors only when it is edited.
   */
  readonly ibanError = computed(() => {
    const country = this.ibanCountry();
    const iban = this.formValue().iban;
    return country && iban ? ibanProblem(iban, country) : null;
  });

  readonly canSubmit = computed(() =>
    canSubmitBankDetails(this.formValue(), this.bankCountryAlpha2())
  );

  constructor() {
    super();
    this.formGroup.valueChanges
      .pipe(takeUntil(this.destroyed$))
      .subscribe(() => {
        this.normalize();
        this.formValue.set(this.formGroup.getRawValue());
      });
  }

  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    combineLatest([
      this.partnerClient.employeeClient.getCurrentEmployee(),
      this.payoutDetailsService.getMine(),
      // Every country, not only the serviced ones: a cleaner may work here and
      // bank elsewhere, and the server supports the cross-border payout.
      this.partnerClient.countryClient
        .getOverview()
        .pipe(catchError(() => of([] as CountryListItem[]))),
    ])
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (!response) {
          this.loadFailed.set(true);
          return;
        }

        const [employee, payoutDetails, countries] = response;
        this.employeeId.set(employee.id ?? '');
        // `?? []` because the generated client answers a 200 whose body is not a JSON array — an
        // empty body, a `{}`, a bare `null` — with NULL rather than an empty list, and a 204 falls
        // past every branch to the same. See `processGetOverview` on CountryClient: it ends
        // `result200 = null as any` while its declared `CountryListItem[]` return type says that
        // cannot happen. The `catchError` above does not cover it — null is not an error, so it
        // reaches here untouched and `.map()` throws inside the subscriber, past `finalize`, so the
        // page is left with the form unpatched, no error state and a spinner already cleared.
        this.countries.set(
          (countries ?? []).map((country) => this.toOption(country))
        );
        this.countryAlpha2.set(
          new Map(
            (countries ?? []).flatMap((country) => {
              const alpha2 = this.alpha2Of(country);
              return country.id && alpha2 ? [[country.id, alpha2] as const] : [];
            })
          )
        );
        this.formGroup.setValue(
          mapPayoutDetailsToBankForm(payoutDetails, employee.countryId)
        );
      });
  }

  retry(): void {
    this.load();
  }

  onSubmit(): void {
    if (this.saving() || !this.canSubmit()) {
      return;
    }

    const employeeId = this.employeeId();
    if (!employeeId) {
      this.snackbarService.showErrorTranslated('global.messages.profile.not_loaded');
      return;
    }

    this.saving.set(true);

    this.partnerClient.employeeClient
      .updateBankDetails(
        createUpdateBankDetailsCommand(
          employeeId,
          this.formGroup.getRawValue(),
          this.bankCountryAlpha2()
        )
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (!response) {
          return;
        }

        this.snackbarService.showSuccessTranslated('global.messages.profile.bank_details_saved');
        this.store.dispatch(checkEmployeeCurrent());
      });
  }

  private normalize(): void {
    for (const field of NORMALIZED_BANK_FIELDS) {
      const control = this.formGroup.controls[field];
      const normalized = BANK_FIELD_NORMALIZERS[field](control.value);
      if (normalized !== control.value) {
        control.setValue(normalized, { emitEvent: false });
      }
    }
  }

  /** `isoAlpha2`, or an alpha-2 `isoCode` — the seed stores `isoCode` alpha-3. */
  private alpha2Of(country: CountryListItem): string | undefined {
    const code = (country.isoAlpha2 || country.isoCode)?.trim().toUpperCase();
    return code && /^[A-Z]{2}$/.test(code) ? code : undefined;
  }

  private toOption(country: CountryListItem): ICleansiaSelectOption {
    const translated = country.translations?.[this.translate.currentLang]?.name;
    const name = translated ?? country.name ?? country.isoCode ?? '';
    const iso = country.isoCode ?? '';

    return {
      label: iso ? `${name} (${iso})` : name,
      value: country.id ?? '',
    };
  }
}
