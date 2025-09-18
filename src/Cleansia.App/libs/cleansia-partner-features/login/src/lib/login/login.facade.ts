import { inject, Injectable } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AuthService,
  CleansiaPartnerRoute,
  JwtTokenResponse,
  SnackbarService,
} from '@cleansia/services';
import { loadUserCurrent } from '@cleansia/stores';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class LoginFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  formGroup = this.createFormGroup();

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
            this.router.navigate([CleansiaPartnerRoute.CONFIRM_EMAIL], {
              queryParams: { email },
            });
            return;
          }
          this.authService.setSession(authResult);
          this.store.dispatch(loadUserCurrent());
          this.router.navigate([CleansiaPartnerRoute.DASHBOARD]);
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
