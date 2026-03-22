import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CleansiaCustomerRoute } from '@cleansia/services';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { ForgotPasswordFacade } from './forgot-password.facade';
import { checkIfPasswordsValid, PasswordCheck } from './forgot-password.models';

@Component({
  selector: 'cleansia-customer-forgot-password',
  templateUrl: './forgot-password.component.html',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaButtonComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaDynamicBackgroundComponent,
    CleansiaTitleComponent,
  ],
  providers: [ForgotPasswordFacade],
})
export class ForgotPasswordComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  protected readonly translate = inject(TranslateService);
  protected readonly facade = inject(ForgotPasswordFacade);

  protected readonly loading = signal(false);

  routes = CleansiaCustomerRoute;

  get hasPasswordInput(): boolean {
    return !!this.facade.passwordFormGroup.get('password')?.value;
  }

  get hasConfirmPasswordInput(): boolean {
    return !!this.facade.passwordFormGroup.get('confirmPassword')?.value;
  }

  get isPasswordValid(): PasswordCheck {
    const password = this.facade.passwordFormGroup.get('password')?.value;
    if (!password) return checkIfPasswordsValid('');
    return checkIfPasswordsValid(password);
  }

  get isConfirmPasswordValid(): PasswordCheck {
    const confirmPassword = this.facade.passwordFormGroup.get('confirmPassword')?.value;
    if (!confirmPassword) return checkIfPasswordsValid('');
    const password = this.facade.passwordFormGroup.get('password')?.value;
    return checkIfPasswordsValid(confirmPassword, password);
  }

  get resendCodeTimeout(): string {
    return `00:${
      this.facade.resendCodeTimeout > 9
        ? this.facade.resendCodeTimeout
        : '0' + this.facade.resendCodeTimeout
    }`;
  }

  ngOnInit(): void {
    this.route.queryParams.subscribe((params) => {
      if (params['email'] && params['code']) {
        this.facade.updateFormsFromEmailData(params['email'], params['code']);
      }
    });
  }

  onSendCode(): void {
    this.facade.sendCode();
  }

  onChangePassword(): void {
    if (this.facade.passwordFormGroup.invalid) {
      return;
    }
    this.facade.changePassword();
  }
}
