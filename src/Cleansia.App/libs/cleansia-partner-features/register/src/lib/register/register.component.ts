import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPartnerRoute } from '@cleansia/services';
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
    CleansiaTelephoneComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaDynamicBackgroundComponent,
  ],
  providers: [RegisterFacade],
})
export class RegisterComponent {
  protected readonly facade = inject(RegisterFacade);

  protected routes = CleansiaPartnerRoute;

  get isPasswordValid(): PasswordCheck {
    const password = this.facade.formGroup.get('password')?.value;
    if (!this.facade.formGroup || !password) {
      return checkIfPasswordsValid('');
    }
    return checkIfPasswordsValid(password);
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
