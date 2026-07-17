import { inject, Injectable, signal } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerAuthService } from '@cleansia/customer-services';
import { loadCustomerUser } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { takeUntil } from 'rxjs';

@Injectable()
export class ConfirmEmailFacade extends UnsubscribeControlDirective {
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly store = inject(Store);
  private readonly snackbarService = inject(SnackbarService);

  readonly isResendDisabled = signal(false);
  readonly resendCodeTimeout = signal(30);
  readonly emailKnown = signal(false);

  formGroup: FormGroup = this.createConfirmEmailFormGroup();

  setEmail(email: string): void {
    const emailControl = this.formGroup.get('email');
    if (!emailControl) {
      return;
    }
    emailControl.setValue(email);
    // The query param is caller-controlled: only hide the email field when the
    // value actually passes the validators — a mangled/forged link otherwise
    // locks the user behind an invisible invalid control.
    this.emailKnown.set(emailControl.valid);
  }

  confirmEmail(): void {
    if (this.formGroup.invalid) {
      this.snackbarService.showErrorTranslated(
        'validation.common.not_all_fields_filled'
      );
      return;
    }

    const { code, email } = this.formGroup.value;
    this.authService.confirmUserEmail(code, email).pipe(
      takeUntil(this.destroyed$)
    ).subscribe({
      next: () => {
        this.snackbarService.showSuccessTranslated('auth.confirm_email.success');
        this.store.dispatch(loadCustomerUser());
        this.router.navigate([CleansiaCustomerRoute.ORDERS]);
      },
      error: (err) => {
        this.snackbarService.showApiError(err, 'auth.confirm_email.error');
      },
    });
  }

  resendCode(): void {
    const emailControl = this.formGroup.get('email');
    if (!emailControl || emailControl.invalid) {
      this.snackbarService.showErrorTranslated(
        'validation.common.not_all_fields_filled'
      );
      return;
    }

    this.isResendDisabled.set(true);
    this.resendCodeTimeout.set(30);

    const interval = setInterval(() => {
      this.resendCodeTimeout.update(v => v - 1);
      if (this.resendCodeTimeout() <= 0) {
        clearInterval(interval);
        this.isResendDisabled.set(false);
        this.resendCodeTimeout.set(30);
      }
    }, 1000);

    this.authService.resendEmailConfirmation(emailControl.value).pipe(
      takeUntil(this.destroyed$)
    ).subscribe({
      next: () => {
        this.snackbarService.showSuccessTranslated('auth.confirm_email.resend_success');
      },
      error: (err) => {
        this.snackbarService.showApiError(err, 'auth.confirm_email.resend_error');
        clearInterval(interval);
        this.isResendDisabled.set(false);
        this.resendCodeTimeout.set(30);
      },
    });
  }

  private createConfirmEmailFormGroup(): FormGroup {
    return new FormGroup({
      code: new FormControl<string | null>(null, [
        Validators.required,
        Validators.minLength(6),
        Validators.maxLength(6),
      ]),
      email: new FormControl<string | null>(null, [
        Validators.required,
        Validators.email,
      ]),
    });
  }
}
