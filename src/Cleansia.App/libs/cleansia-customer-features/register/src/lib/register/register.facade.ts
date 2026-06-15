import { inject, Injectable, signal } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerAuthService,
  CustomerClient,
  JwtTokenResponse,
  ValidateReferralQuery,
} from '@cleansia/customer-services';
import { loadCustomerUser } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom, takeUntil } from 'rxjs';

/**
 * Discriminated union for the optional referral-code input on the signup form.
 * Mirrors the booking wizard's referral state machine — purely a UX concern;
 * the backend re-validates at registration time and a bad code never blocks
 * signup.
 */
export type ReferralUiState =
  | { kind: 'idle' }
  | { kind: 'validating' }
  | { kind: 'valid'; referrerFirstName: string | null }
  | { kind: 'invalid'; error: string | null };

@Injectable()
export class RegisterFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  formGroup = this.createFormGroup();

  // ─── Referral input state ───────────────────────────────────
  //
  // The signup form has an optional "Add a referral code" entry row that
  // opens a Wolt-style modal dialog. The dialog calls `validateReferralCodeNow`
  // exactly once on Apply — no debounced auto-validation. The state machine
  // mirrors the booking wizard's; we keep both code-paths independent so this
  // facade owns its own contract.
  readonly referralCode = signal('');
  readonly referralState = signal<ReferralUiState>({ kind: 'idle' });

  /**
   * Apply-button handler from the referral dialog. Single backend call, no
   * debounce. Empty input resets to idle without touching the network.
   * Returns the resolved state so the dialog can react to it.
   */
  async validateReferralCodeNow(code: string): Promise<ReferralUiState> {
    const normalized = code.trim().toUpperCase();
    if (!normalized) {
      this.referralState.set({ kind: 'idle' });
      this.setReferralCode('');
      return { kind: 'idle' };
    }
    this.referralState.set({ kind: 'validating' });
    try {
      const resp = await firstValueFrom(
        this.customerClient.referralClient.validate(
          new ValidateReferralQuery({
            code: normalized,
          }),
        ),
      );
      const newState: ReferralUiState = resp.isValid
        ? { kind: 'valid', referrerFirstName: resp.referrerFirstName ?? null }
        : { kind: 'invalid', error: resp.errorCode ?? null };
      this.referralState.set(newState);
      if (newState.kind === 'valid') {
        this.setReferralCode(normalized);
      }
      return newState;
    } catch {
      const newState: ReferralUiState = { kind: 'invalid', error: null };
      this.referralState.set(newState);
      return newState;
    }
  }

  /** Wipes the applied referral state — used by the row's clear-X button. */
  clearReferralCode(): void {
    this.setReferralCode('');
    this.referralState.set({ kind: 'idle' });
  }

  /**
   * Pre-applies a code captured from the /r/{code} landing URL. The code is
   * mirrored into the form BEFORE the single-shot validation so it survives
   * to authService.register even when validation fails or the network is
   * down — the backend skips bad codes fail-soft and never blocks signup.
   */
  async applyReferralCodeFromUrl(
    code: string | null | undefined
  ): Promise<void> {
    const normalized = code?.trim().toUpperCase();
    if (!normalized) return;
    this.setReferralCode(normalized);
    await this.validateReferralCodeNow(normalized);
  }

  /** Mirror the value into both the signal and the form control. */
  private setReferralCode(value: string): void {
    this.referralCode.set(value);
    this.formGroup.get('referralCode')?.setValue(value, { emitEvent: false });
  }

  register() {
    if (this.formGroup.invalid) {
      return this.snackbarService.showError(
        this.translate.instant('validation.common.not_all_fields_filled')
      );
    }

    const { email, password, firstName, lastName, referralCode } = this.formGroup.value;
    // Bad/empty referral codes are NOT a blocker per the spec — we send the
    // raw value (when non-empty) and let the backend silently skip on failure.
    const trimmedReferral = (referralCode as string | undefined)?.trim();
    this.authService
      .register(email, password, firstName, lastName, trimmedReferral || undefined)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.snackbarService.showSuccessTranslated('auth.register.success');
          this.router.navigate([CleansiaCustomerRoute.CONFIRM_EMAIL], {
            queryParams: { email },
          });
        },
        error: (err) => {
          this.snackbarService.showApiError(err, 'auth.register.error');
        },
      });
  }

  googleRegister(credential: string) {
    const decoded = JSON.parse(atob(credential.split('.')[1]));
    const { sub: googleId, email, given_name: firstName, family_name: lastName } = decoded;

    this.authService
      .authenticateWithGoogle(credential, googleId, email, firstName || '', lastName || '')
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (authResult: JwtTokenResponse) => {
          this.authService.setSession(authResult);
          this.store.dispatch(loadCustomerUser());
          this.snackbarService.showSuccessTranslated('auth.login.success');
          this.router.navigate([CleansiaCustomerRoute.ORDERS]);
        },
        error: (err) => {
          this.snackbarService.showApiError(err, 'auth.register.error');
        },
      });
  }

  private createFormGroup(): FormGroup {
    const passwordPattern = /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;

    return new FormGroup({
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
      // Optional — never required. Bad codes don't block submit.
      referralCode: new FormControl(''),
      terms: new FormControl(false, [Validators.requiredTrue]),
    });
  }
}
