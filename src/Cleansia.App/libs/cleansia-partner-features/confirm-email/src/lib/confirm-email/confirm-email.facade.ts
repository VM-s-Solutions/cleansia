import { inject, Injectable, signal } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { PartnerAuthService } from '@cleansia/partner-services';
import { CustomValidators, SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class ConfirmEmailFacade extends UnsubscribeControlDirective {
  private readonly authService = inject(PartnerAuthService);
  private readonly snackbarService = inject(SnackbarService);

  readonly isResendDisabled = signal(false);
  readonly resendCodeTimeout = signal(30);
  readonly emailKnown = signal(false);
  readonly confirming = signal(false);
  /** The confirmation sets the password its confirmer chooses; none is asked right after registering. */
  readonly passwordAsked = signal(true);

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
    this.askForPassword(!(emailControl.valid && this.authService.holdsRegisteredPasswordFor(email)));
  }

  // The boxes submit as they fill in; with a password still to choose that waits for the button.
  confirmWhenComplete(): void {
    if (this.formGroup.valid && !this.passwordMismatch()) {
      this.confirmEmail();
    }
  }

  // The boxes complete a code again when AutoFill delivers it twice; one confirm at a time keeps the
  // second from spending an attempt.
  confirmEmail(): void {
    if (this.confirming()) {
      return;
    }

    if (this.formGroup.get('code')?.invalid || this.formGroup.get('email')?.invalid) {
      this.snackbarService.showErrorTranslated(
        'validation.common.not_all_fields_filled'
      );
      return;
    }

    if (this.formGroup.invalid || this.passwordMismatch()) {
      this.snackbarService.showErrorTranslated('pages.email_confirm.password_invalid');
      return;
    }

    const { code, email, password } = this.formGroup.getRawValue();
    this.confirming.set(true);
    this.authService
      .confirmUserEmail(code, email, this.passwordAsked() ? password : undefined)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.confirming.set(false))
      )
      .subscribe();
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
      this.resendCodeTimeout.update((v) => v - 1);
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

  private passwordMismatch(): boolean {
    const { password, confirmPassword } = this.formGroup.getRawValue();
    return this.passwordAsked() && password !== confirmPassword;
  }

  private askForPassword(asked: boolean): void {
    this.passwordAsked.set(asked);
    for (const name of ['password', 'confirmPassword']) {
      const control = this.formGroup.get(name);
      if (asked) {
        control?.enable({ emitEvent: false });
      } else {
        control?.disable({ emitEvent: false });
      }
    }
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
      password: new FormControl<string | null>(null, [
        Validators.required,
        CustomValidators.password(),
      ]),
      confirmPassword: new FormControl<string | null>(null, [Validators.required]),
    });
  }
}
