import { inject, Injectable, signal } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import { loadCustomerUser } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { takeUntil } from 'rxjs';

@Injectable()
export class ConfirmEmailFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly store = inject(Store);
  private readonly snackbarService = inject(SnackbarService);

  readonly isResendDisabled = signal(false);
  readonly resendCodeTimeout = signal(30);

  formGroup: FormGroup = this.createConfirmEmailFormGroup();

  confirmEmail(): void {
    if (this.formGroup.invalid) {
      return;
    }

    const code = this.formGroup.value.code;
    this.authService.confirmUserEmail(code).pipe(
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

  resendCode(email: string): void {
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

    this.authService.resendEmailConfirmation(email).pipe(
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
    });
  }
}
