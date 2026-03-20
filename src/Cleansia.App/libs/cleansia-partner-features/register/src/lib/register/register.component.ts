import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { selectLoading } from '@cleansia/partner-stores';
import { Store } from '@ngrx/store';
import { TranslatePipe } from '@ngx-translate/core';
import { FloatLabelModule } from 'primeng/floatlabel';
import { RegisterFacade } from './register.facade';
import { PasswordCheck, checkIfPasswordsValid } from './register.models';

@Component({
  selector: 'cleansia-partner-register',
  templateUrl: './register.component.html',
  imports: [
    RouterLink,
    CommonModule,
    TranslatePipe,
    FloatLabelModule,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaDynamicBackgroundComponent,
  ],
  providers: [RegisterFacade],
})
export class RegisterComponent {
  private readonly store = inject(Store);
  protected readonly facade = inject(RegisterFacade);

  protected readonly loading = toSignal(this.store.select(selectLoading));

  protected routes = CleansiaPartnerRoute;

  get isPasswordValid(): PasswordCheck {
    const password = this.facade.formGroup.get('password')?.value;
    if (!this.facade.formGroup || !password) {
      return checkIfPasswordsValid('');
    }
    return checkIfPasswordsValid(password);
  }

  get hasPasswordInput(): boolean {
    return (this.facade.formGroup.get('password')?.value?.length ?? 0) > 0;
  }

  get hasConfirmPasswordInput(): boolean {
    return (this.facade.formGroup.get('confirmPassword')?.value?.length ?? 0) > 0;
  }

  get isConfirmPasswordValid(): PasswordCheck {
    const confirmPassword = this.facade.formGroup.get('confirmPassword')?.value;
    if (!this.facade.formGroup || !confirmPassword) {
      return checkIfPasswordsValid('');
    }
    const password = this.facade.formGroup.get('password')?.value;
    return checkIfPasswordsValid(confirmPassword, password);
  }
}
