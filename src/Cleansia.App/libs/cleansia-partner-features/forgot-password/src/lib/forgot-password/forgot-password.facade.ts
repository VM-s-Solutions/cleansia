import { inject, Injectable } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  ChangePasswordCommand,
  CleansiaPartnerRoute,
  Client,
  RequestPasswordChangeCommand,
  SnackbarService,
} from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class ForgotPasswordFacade extends UnsubscribeControlDirective {
  private readonly client = inject(Client);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  emailFormGroup: FormGroup = this.createEmailFormGroup();
  passwordFormGroup: FormGroup = this.createPasswordFormGroup();

  isEmailSent = false;
  isResendDisabled = false;
  resendCodeTimeout = 30;

  get passwordMismatchError(): boolean {
    const password = this.passwordFormGroup.get('password');
    const confirmPassword = this.passwordFormGroup.get('confirmPassword');
    return (
      (password?.value !== confirmPassword?.value &&
        confirmPassword?.touched &&
        confirmPassword?.dirty) ??
      false
    );
  }

  sendCode(): void {
    if (this.emailFormGroup.invalid) {
      return this.snackbarService.showError(
        this.translate.instant('pages.forgot_password.email_invalid')
      );
    }

    const email = this.emailFormGroup.value.email;
    const resendCodeCooldown = 30_000;
    this.isResendDisabled = true;

    this.client.userClient
      .requestPasswordChange(
        new RequestPasswordChangeCommand({
          email,
        })
      )
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.isEmailSent = true;
          const interval = setInterval(() => this.resendCodeTimeout--, 1000);
          setTimeout(() => {
            this.isResendDisabled = false;
            this.resendCodeTimeout = 30;
            clearInterval(interval);
          }, resendCodeCooldown);
        },
      });
  }

  changePassword(): void {
    if (this.passwordFormGroup.invalid || this.passwordMismatchError) {
      return this.snackbarService.showError(
        this.translate.instant('pages.forgot_password.password_invalid')
      );
    }

    const { code, password } = this.passwordFormGroup.value;
    const email = this.emailFormGroup.value.email;

    this.client.userClient
      .changePassword(
        new ChangePasswordCommand({ email, code, newPassword: password })
      )
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.forgot_password.password_changed')
          );
          this.isEmailSent = false;
          this.emailFormGroup.reset();
          this.passwordFormGroup.reset();
          this.router.navigate([CleansiaPartnerRoute.LOGIN]);
        },
      });
  }

  updateFormsFromEmailData(email: string, code: string): void {
    this.emailFormGroup.patchValue({ email });
    this.passwordFormGroup.patchValue({ code });
    this.isEmailSent = true;
  }

  private createEmailFormGroup(): FormGroup {
    return new FormGroup({
      email: new FormControl(null, [Validators.required, Validators.email]),
    });
  }

  private createPasswordFormGroup(): FormGroup {
    const passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;
    return new FormGroup({
      code: new FormControl(null, [
        Validators.required,
        Validators.minLength(6),
        Validators.maxLength(6),
      ]),
      password: new FormControl(null, [
        Validators.required,
        Validators.pattern(passwordPattern),
      ]),
      confirmPassword: new FormControl(null, [
        Validators.required,
        Validators.pattern(passwordPattern),
      ]),
    });
  }
}
