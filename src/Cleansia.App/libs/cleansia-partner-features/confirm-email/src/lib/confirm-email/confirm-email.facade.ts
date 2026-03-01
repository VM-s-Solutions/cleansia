import { inject, Injectable } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { PartnerAuthService, PartnerClient } from '@cleansia/partner-services';
import { loadUserCurrent } from '@cleansia/partner-stores';
import { Store } from '@ngrx/store';
import { takeUntil } from 'rxjs';

@Injectable()
export class ConfirmEmailFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly router = inject(Router);
  private readonly authService = inject(PartnerAuthService);
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
        this.store.dispatch(loadUserCurrent());
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
    const formGroup = new FormGroup({
      code: new FormControl<string | null>(null, [
        Validators.required,
        Validators.minLength(6),
        Validators.maxLength(6),
      ]),
    });

    return formGroup;
  }
}
