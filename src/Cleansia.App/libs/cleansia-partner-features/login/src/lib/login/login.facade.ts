import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  JwtTokenResponse,
  PartnerAuthService,
} from '@cleansia/partner-services';
import { selectLoading } from '@cleansia/partner-stores';
import { CleansiaPartnerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { takeUntil } from 'rxjs';

@Injectable()
export class LoginFacade extends UnsubscribeControlDirective {
  private readonly fb = inject(FormBuilder);
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(PartnerAuthService);
  private readonly snackbarService = inject(SnackbarService);

  formGroup = this.createFormGroup();
  loading = toSignal(this.store.select(selectLoading));

  login(): void {
    if (this.formGroup.invalid) {
      this.snackbarService.showErrorTranslated('validation.common.not_all_fields_filled');
      return;
    }
    const { email, password, rememberMe } = this.formGroup.getRawValue();
    this.authService
      .login(email, password, rememberMe)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (authResult: JwtTokenResponse) => {
          if (!authResult.isEmailConfirmed) {
            this.router.navigate([CleansiaPartnerRoute.CONFIRM_EMAIL], {
              queryParams: { email },
            });
            return;
          }
          this.authService.setSession(authResult);
          this.router.navigate([CleansiaPartnerRoute.ORDERS]);
        },
        error: (err) => {
          this.snackbarService.showApiError(err, 'auth.login.error');
        },
      });
  }

  private createFormGroup() {
    return this.fb.nonNullable.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]],
      rememberMe: [false],
    });
  }
}
