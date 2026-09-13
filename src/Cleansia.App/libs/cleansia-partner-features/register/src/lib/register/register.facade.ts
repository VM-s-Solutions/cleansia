import { computed, inject, Injectable, signal } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  MarketListItem,
  PartnerAuthService,
  PartnerClient,
  SignupConsentService,
} from '@cleansia/partner-services';
import { CleansiaPartnerRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, of, takeUntil } from 'rxjs';
import { defaultMarket, marketOptionLabel } from './register.models';

@Injectable()
export class RegisterFacade extends UnsubscribeControlDirective {
  private readonly router = inject(Router);
  private readonly authService = inject(PartnerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly signupConsent = inject(SignupConsentService);
  private readonly partnerClient = inject(PartnerClient);

  formGroup = this.createFormGroup();

  private readonly markets = signal<MarketListItem[]>([]);
  private readonly lang = signal(
    this.translate.currentLang || this.translate.getDefaultLang()
  );

  readonly marketOptions = computed<ICleansiaSelectOption[]>(() => {
    const lang = this.lang();
    return this.markets()
      .filter((market): market is MarketListItem & { countryId: string } => !!market.countryId)
      .map((market) => ({ value: market.countryId, label: marketOptionLabel(market, lang) }));
  });

  readonly hasMarketChoice = computed(() => this.marketOptions().length >= 2);

  constructor() {
    super();
    this.translate.onLangChange
      .pipe(takeUntil(this.destroyed$))
      .subscribe(({ lang }) => this.lang.set(lang));
    this.loadMarkets();
  }

  register() {
    if (this.formGroup.invalid) {
      return this.snackbarService.showError(
        this.translate.instant('validation.common.not_all_fields_filled')
      );
    }

    const { email, password, firstName, lastName, countryId } =
      this.formGroup.value;
    const termsAccepted = this.formGroup.get('terms')?.value === true;
    this.authService
      .registerEmployee(email, password, firstName, lastName, countryId)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          if (termsAccepted) {
            this.signupConsent.record(email);
          }
          this.router.navigate([CleansiaPartnerRoute.CONFIRM_EMAIL], {
            queryParams: { email },
          });
        },
      });
  }

  private loadMarkets(): void {
    this.partnerClient.marketClient
      .getOverview()
      .pipe(
        catchError(() => of<MarketListItem[]>([])),
        takeUntil(this.destroyed$)
      )
      .subscribe((markets) => {
        this.markets.set(markets);
        this.formGroup
          .get('countryId')
          ?.setValue(defaultMarket(markets)?.countryId ?? null);
      });
  }

  private createFormGroup(): FormGroup {
    const passwordPattern = /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;

    const formGroup = new FormGroup({
      firstName: new FormControl('', [
        Validators.required,
        Validators.maxLength(50),
      ]),
      lastName: new FormControl('', [
        Validators.required,
        Validators.maxLength(50),
      ]),
      email: new FormControl('', [Validators.required, Validators.email]),
      password: new FormControl('', [
        Validators.required,
        Validators.pattern(passwordPattern),
      ]),
      confirmPassword: new FormControl('', [
        Validators.required,
        Validators.pattern(passwordPattern),
      ]),
      // phone: new FormControl('', [Validators.required]),
      // street: new FormControl('', [Validators.required]),
      // city: new FormControl('', [Validators.required]),
      // zipCode: new FormControl('', [Validators.required]),
      // country: new FormControl('Czech Republic', [Validators.required]),
      // ico: new FormControl('', [
      //   Validators.required,
      //   Validators.pattern(/^\d{8}$/),
      // ]),
      terms: new FormControl(false, [Validators.requiredTrue]),
      countryId: new FormControl<string | null>(null),
    });
    return formGroup;
  }
}
