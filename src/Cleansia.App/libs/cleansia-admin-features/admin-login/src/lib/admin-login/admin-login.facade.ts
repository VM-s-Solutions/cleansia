import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminAuthService, JwtTokenResponse } from '@cleansia/admin-services';
import { loadUserCurrent, selectLoading } from '@cleansia/admin-stores';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class AdminLoginFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(AdminAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  formGroup = this.createFormGroup();
  loading = toSignal(this.store.select(selectLoading));

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
            this.snackbarService.showError(
              this.translate.instant('validation.auth.email_not_confirmed')
            );
            return;
          }

          // Check if user has admin/editor role
          this.authService.setSession(authResult);
          this.store.dispatch(loadUserCurrent());

          if (!this.authService.isAdminOrEditor()) {
            this.snackbarService.showError(
              this.translate.instant('validation.auth.admin_access_required')
            );
            this.authService.logout();
            return;
          }

          this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
        },
      });
  }

  private createFormGroup(): FormGroup {
    const formGroup = new FormGroup({
      email: new FormControl('', [Validators.required, Validators.email]),
      password: new FormControl('', [
        Validators.required,
        Validators.minLength(6),
      ]),
      rememberMe: new FormControl(false),
    });
    return formGroup;
  }
}
