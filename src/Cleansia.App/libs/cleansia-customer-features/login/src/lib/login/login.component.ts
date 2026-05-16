import { ChangeDetectionStrategy, Component, inject, AfterViewInit, ElementRef, viewChild, NgZone, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
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
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { LoginFacade } from './login.facade';

@Component({
  selector: 'cleansia-customer-login',
  templateUrl: './login.component.html',
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
    CleansiaDynamicBackgroundComponent,
  ],
  providers: [LoginFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent implements AfterViewInit {
  protected readonly facade = inject(LoginFacade);
  protected routes = CleansiaCustomerRoute;
  private readonly zone = inject(NgZone);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  googleBtnRef = viewChild<ElementRef>('googleBtn');

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
      callback: (response: { credential: string }) => {
        this.zone.run(() => this.facade.googleLogin(response.credential));
      },
    });

    const btnEl = this.googleBtnRef()?.nativeElement;
    if (btnEl) {
      // GSI's `width` rejects percentage strings ("Provided button width is
      // invalid: 100%") — it wants an integer pixel value, max 400. Measure
      // the container at render time; clamp so we don't blow past GSI's cap.
      // Falls back to 400 (the max) if the container has no width yet, which
      // can happen if the parent is display:none on first paint.
      const measured = (btnEl as HTMLElement).clientWidth;
      const width = Math.min(measured > 0 ? measured : 400, 400);
      google.accounts.id.renderButton(btnEl, {
        theme: 'outline',
        size: 'large',
        width,
        text: 'continue_with',
        shape: 'rectangular',
      });
    }
  }
}
