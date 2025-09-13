import { inject, Injectable } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AuthService,
  CleansiaPartnerRoute,
  SnackbarService,
} from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class RegisterFacade extends UnsubscribeControlDirective {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
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
          this.router.navigate([CleansiaPartnerRoute.CONFIRM_EMAIL], {
            queryParams: { email },
          });
        },
      });
  }

  private createFormGroup(): FormGroup {
    const passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

    const formGroup = new FormGroup({
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
      // phone: new FormControl('', [Validators.required]),
      // street: new FormControl('', [Validators.required]),
      // city: new FormControl('', [Validators.required]),
      // zipCode: new FormControl('', [Validators.required]),
      // country: new FormControl('Czech Republic', [Validators.required]),
      // ico: new FormControl('', [
      //   Validators.required,
      //   Validators.pattern(/^\d{8}$/),
      // ]),
      terms: new FormControl(false, [Validators.requiredTrue]),
    });
    return formGroup;
  }
}
