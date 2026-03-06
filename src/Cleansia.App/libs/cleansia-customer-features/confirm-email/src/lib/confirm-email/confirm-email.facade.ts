import { inject, Injectable } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import { loadCustomerUser } from '@cleansia/customer-stores';
import { Store } from '@ngrx/store';
import { takeUntil } from 'rxjs';

@Injectable()
export class ConfirmEmailFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly store = inject(Store);

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
        this.store.dispatch(loadCustomerUser());
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
        const interval = setInterval(() => this.resendCodeTimeout--, 1000);
        setTimeout(() => {
          this.isResendDisabled = false;
          this.resendCodeTimeout = 30;
          clearInterval(interval);
        }, resendCodeCooldown);
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
