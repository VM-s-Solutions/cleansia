import { Component, inject } from '@angular/core';
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
import { TranslatePipe } from '@ngx-translate/core';
import { LoginFacade } from './login.facade';

@Component({
  selector: 'cleansia-partner-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  standalone: true,
  imports: [
    RouterLink,
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
  providers: [LoginFacade],
})
export class LoginComponent {
  protected readonly facade = inject(LoginFacade);

  protected routes = CleansiaPartnerRoute;
}
