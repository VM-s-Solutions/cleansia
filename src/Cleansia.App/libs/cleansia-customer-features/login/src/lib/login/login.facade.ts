import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerAuthService,
} from '@cleansia/customer-services';
import { JwtTokenResponse } from '@cleansia/partner-services';
import { loadCustomerUser, selectCustomerLoading } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, GuestOrderService, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class LoginFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly guestOrderService = inject(GuestOrderService);

  formGroup = this.createFormGroup();
  loading = toSignal(this.store.select(selectCustomerLoading));

  login() {
    if (this.formGroup.invalid) {
      return this.snackbarService.showError(
        this.translate.instant('validation.common.not_all_fields_filled')
      );
    }
    const email = this.formGroup.get('email')?.value;
    const password = this.formGroup.get('password')?.value;
    const rememberMe = this.formGroup.get('rememberMe')?.value;
    this.authService
      .login(email, password, rememberMe)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (authResult: JwtTokenResponse) => {
          if (!authResult.isEmailConfirmed) {
            this.snackbarService.showSuccessTranslated('auth.login.email_not_confirmed');
            this.router.navigate([CleansiaCustomerRoute.CONFIRM_EMAIL], {
              queryParams: { email },
            });
            return;
          }
          this.authService.setSession(authResult);
          this.guestOrderService.clear();
          this.store.dispatch(loadCustomerUser());
          this.snackbarService.showSuccessTranslated('auth.login.success');
          this.router.navigate([CleansiaCustomerRoute.ORDERS]);
        },
        error: (err) => {
          this.snackbarService.showApiError(err, 'auth.login.error');
        },
      });
  }

  googleLogin(credential: string) {
    const decoded = JSON.parse(atob(credential.split('.')[1]));
    const { sub: googleId, email, given_name: firstName, family_name: lastName } = decoded;

    this.authService
      .authenticateWithGoogle(credential, googleId, email, firstName || '', lastName || '')
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (authResult: JwtTokenResponse) => {
          this.authService.setSession(authResult);
          this.guestOrderService.clear();
          this.store.dispatch(loadCustomerUser());
          this.snackbarService.showSuccessTranslated('auth.login.success');
          this.router.navigate([CleansiaCustomerRoute.ORDERS]);
        },
        error: (err) => {
          this.snackbarService.showApiError(err, 'auth.login.error');
        },
      });
  }

  private createFormGroup(): FormGroup {
    return new FormGroup({
      email: new FormControl('', [Validators.required, Validators.email]),
      password: new FormControl('', [Validators.required]),
      rememberMe: new FormControl(false),
    });
  }
}
