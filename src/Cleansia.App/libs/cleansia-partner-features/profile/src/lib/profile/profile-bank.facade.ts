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

  readonly canSubmit = computed(() => canSubmitBankDetails(this.formValue()));

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
        this.countries.set(countries.map((country) => this.toOption(country)));
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
      this.snackbarService.showError(
        this.translate.instant('global.messages.profile.not_loaded')
      );
      return;
    }

    this.saving.set(true);

    this.partnerClient.employeeClient
      .updateBankDetails(
        createUpdateBankDetailsCommand(employeeId, this.formGroup.getRawValue())
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

        this.snackbarService.showSuccess(
          this.translate.instant('global.messages.profile.bank_details_saved')
        );
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
