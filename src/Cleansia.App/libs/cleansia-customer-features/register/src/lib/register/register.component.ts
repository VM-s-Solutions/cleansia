import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { selectCustomerLoading } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe } from '@ngx-translate/core';
import { RegisterFacade } from './register.facade';
import { checkIfPasswordsValid, PasswordCheck } from './register.models';

@Component({
  selector: 'cleansia-customer-register',
  templateUrl: './register.component.html',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaDynamicBackgroundComponent,
  ],
  providers: [RegisterFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  private readonly store = inject(Store);
  protected readonly facade = inject(RegisterFacade);
  protected readonly loading = toSignal(this.store.select(selectCustomerLoading));
  protected routes = CleansiaCustomerRoute;

  get hasPasswordInput(): boolean {
    return !!this.facade.formGroup.get('password')?.value;
  }

  get hasConfirmPasswordInput(): boolean {
    return !!this.facade.formGroup.get('confirmPassword')?.value;
  }

  get isPasswordValid(): PasswordCheck {
    const password = this.facade.formGroup.get('password')?.value;
    if (!password) return checkIfPasswordsValid('');
    return checkIfPasswordsValid(password);
  }

  get isConfirmPasswordValid(): PasswordCheck {
    const confirmPassword = this.facade.formGroup.get('confirmPassword')?.value;
    if (!confirmPassword) return checkIfPasswordsValid('');
    const password = this.facade.formGroup.get('password')?.value;
    return checkIfPasswordsValid(confirmPassword, password);
  }
}
