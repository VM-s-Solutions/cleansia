import { Component, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { AdminLoginFacade } from './admin-login.facade';

@Component({
  selector: 'cleansia-admin-login',
  standalone: true,
  imports: [
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaDynamicBackgroundComponent,
  ],
  templateUrl: './admin-login.component.html',
  providers: [AdminLoginFacade],
})
export class AdminLoginComponent {
  protected readonly facade = inject(AdminLoginFacade);
}
