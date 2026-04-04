import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, AfterViewInit, ElementRef, viewChild, NgZone, PLATFORM_ID, inject } from '@angular/core';
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
export class RegisterComponent implements AfterViewInit {
  private readonly store = inject(Store);
  protected readonly facade = inject(RegisterFacade);
  protected readonly loading = toSignal(this.store.select(selectCustomerLoading));
  protected routes = CleansiaCustomerRoute;
  private readonly zone = inject(NgZone);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  googleBtnRef = viewChild<ElementRef>('googleBtn');

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

  ngAfterViewInit() {
    if (!this.isBrowser) return;
    this.initGoogleSignIn();
  }

  private _gsiRetries = 0;
  private readonly _gsiMaxRetries = 20;

  private loadGsiScript(): void {
    if (document.querySelector('script[src*="accounts.google.com/gsi/client"]')) return;
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    document.head.appendChild(script);
  }

  private initGoogleSignIn() {
    this.loadGsiScript();
    const google = (window as any).google;
    if (!google?.accounts?.id) {
      if (this._gsiRetries < this._gsiMaxRetries) {
        this._gsiRetries++;
        setTimeout(() => this.initGoogleSignIn(), 300);
      }
      return;
    }

    google.accounts.id.initialize({
      client_id: '354682423254-boe1nlnb1dbd3m6a013d3nkpo2e9bgiq.apps.googleusercontent.com',
      callback: (response: any) => {
        this.zone.run(() => this.facade.googleRegister(response.credential));
      },
    });

    const btnEl = this.googleBtnRef()?.nativeElement;
    if (btnEl) {
      google.accounts.id.renderButton(btnEl, {
        theme: 'outline',
        size: 'large',
        width: '100%',
        text: 'continue_with',
        shape: 'rectangular',
      });
    }
  }
}
