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
import { catchError, of, takeUntil } from 'rxjs';

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
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error) => {
          this.handleLoginError(error);
          return of(null);
        })
      )
      .subscribe({
        next: (authResult: JwtTokenResponse | null) => {
          if (!authResult) {
            return; // Error already handled in catchError
          }

          if (!authResult.isEmailConfirmed) {
            this.snackbarService.showError(
              this.translate.instant('validation.auth.email_not_confirmed')
            );
            return;
          }

          // Check if user has admin access
          if (!authResult.hasAdminAccess) {
            this.router.navigate(['/unauthorized']);
            return;
          }

          // Set session and navigate to admin
          this.authService.setSession(authResult);
          this.store.dispatch(loadUserCurrent());
          this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
        },
      });
  }

  private handleLoginError(error: unknown): void {
    const errorMessage = this.extractErrorMessage(error);

    // Show the translated error or a generic message
    const translatedMessage = this.translate.instant(errorMessage);
    if (translatedMessage !== errorMessage) {
      this.snackbarService.showError(translatedMessage);
    } else {
      this.snackbarService.showApiError(error, 'validation.auth.login_failed');
    }
  }

  private extractErrorMessage(error: unknown): string {
    const apiError = error as {
      result?: { detail?: string; title?: string };
      response?: string;
      message?: string;
    };

    // Try to get error from result
    if (apiError.result?.detail) {
      return apiError.result.detail;
    }
    if (apiError.result?.title) {
      return apiError.result.title;
    }

    // Try to parse response string
    if (apiError.response) {
      try {
        const parsed = JSON.parse(apiError.response) as {
          detail?: string;
          title?: string;
        };
        return parsed.detail || parsed.title || '';
      } catch {
        return apiError.response;
      }
    }

    return apiError.message || '';
  }

  private createFormGroup(): FormGroup {
    const formGroup = new FormGroup({
      email: new FormControl('', [Validators.required, Validators.email]),
      password: new FormControl('', [Validators.required]),
      rememberMe: new FormControl(false),
    });
    return formGroup;
  }
}
