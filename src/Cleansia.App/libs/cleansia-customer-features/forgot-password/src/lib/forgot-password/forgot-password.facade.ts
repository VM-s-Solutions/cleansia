import { inject, Injectable, signal } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  ChangePasswordCommand,
  CustomerClient,
  RequestPasswordChangeCommand,
} from '@cleansia/customer-services';
import {
  CleansiaCustomerRoute,
  PASSWORD_PATTERN,
  RESEND_CODE_COOLDOWN_SECONDS,
  SnackbarService,
} from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class ForgotPasswordFacade extends UnsubscribeControlDirective {
  private readonly fb = inject(FormBuilder);
  private readonly customerClient = inject(CustomerClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  emailFormGroup: FormGroup = this.createEmailFormGroup();
  passwordFormGroup: FormGroup = this.createPasswordFormGroup();

  readonly loading = signal(false);
  readonly isEmailSent = signal(false);
  readonly isResendDisabled = signal(false);
  readonly resendCodeTimeout = signal(RESEND_CODE_COOLDOWN_SECONDS);

  private resendInterval?: ReturnType<typeof setInterval>;

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
    this.isResendDisabled.set(true);
    this.resendCodeTimeout.set(RESEND_CODE_COOLDOWN_SECONDS);
    this.loading.set(true);

    this.customerClient.userClient
      .requestPasswordChange(
        new RequestPasswordChangeCommand({
          email,
          language:
            this.translate.currentLang || this.translate.getDefaultLang(),
        })
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError((err) => {
          this.snackbarService.showApiError(
            err,
            'pages.forgot_password.send_code_error'
          );
          this.isResendDisabled.set(false);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((result) => {
        if (result === null) {
          return;
        }
        this.isEmailSent.set(true);
        this.startResendCooldown();
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
    this.loading.set(true);

    this.customerClient.userClient
      .changePassword(
        new ChangePasswordCommand({ email, code, newPassword: password })
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError((err) => {
          this.snackbarService.showApiError(
            err,
            'pages.forgot_password.change_password_error'
          );
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((result) => {
        if (result === null) {
          return;
        }
        this.snackbarService.showSuccess(
          this.translate.instant('pages.forgot_password.password_changed')
        );
        this.isEmailSent.set(false);
        this.emailFormGroup.reset();
        this.passwordFormGroup.reset();
        this.router.navigate([CleansiaCustomerRoute.LOGIN]);
      });
  }

  updateFormsFromEmailData(email: string, code: string): void {
    this.emailFormGroup.patchValue({ email });
    this.passwordFormGroup.patchValue({ code });
    this.isEmailSent.set(true);
  }

  private startResendCooldown(): void {
    clearInterval(this.resendInterval);
    this.resendInterval = setInterval(() => {
      this.resendCodeTimeout.update((value) => value - 1);
      if (this.resendCodeTimeout() <= 0) {
        clearInterval(this.resendInterval);
        this.isResendDisabled.set(false);
        this.resendCodeTimeout.set(RESEND_CODE_COOLDOWN_SECONDS);
      }
    }, 1000);
  }

  private createEmailFormGroup(): FormGroup {
    return this.fb.nonNullable.group({
      email: ['', [Validators.required, Validators.email]],
    });
  }

  private createPasswordFormGroup(): FormGroup {
    return this.fb.nonNullable.group({
      code: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]],
      password: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
      confirmPassword: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
    });
  }
}
