import { inject, Injectable } from '@angular/core';
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

  isResendDisabled = false;
  resendCodeTimeout = 30;

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
    this.isResendDisabled = true;
    const resendCodeCooldown = 30_000;
    this.authService.resendEmailConfirmation(email).pipe(
      takeUntil(this.destroyed$)
    ).subscribe({
      next: () => {
        this.snackbarService.showSuccessTranslated('auth.confirm_email.resend_success');
        const interval = setInterval(() => this.resendCodeTimeout--, 1000);
        setTimeout(() => {
          this.isResendDisabled = false;
          this.resendCodeTimeout = 30;
          clearInterval(interval);
        }, resendCodeCooldown);
      },
      error: (err) => {
        this.snackbarService.showApiError(err, 'auth.confirm_email.resend_error');
        this.isResendDisabled = false;
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
