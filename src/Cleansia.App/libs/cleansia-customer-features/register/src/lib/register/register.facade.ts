import { inject, Injectable } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerAuthService } from '@cleansia/customer-services';
import { JwtTokenResponse } from '@cleansia/partner-services';
import { loadCustomerUser } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class RegisterFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  formGroup = this.createFormGroup();

  register() {
    if (this.formGroup.invalid) {
      return this.snackbarService.showError(
        this.translate.instant('validation.common.not_all_fields_filled')
      );
    }

    const { email, password, firstName, lastName } = this.formGroup.value;
    this.authService
      .register(email, password, firstName, lastName)
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
      terms: new FormControl(false, [Validators.requiredTrue]),
    });
  }
}
