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
import {
  APPLE_CLIENT_ID,
  APPLE_ID_SCRIPT_URL,
  APPLE_REDIRECT_PATH,
  AppleNonce,
  CleansiaCustomerRoute,
  createAppleNonce,
  getAppleIdApi,
  GOOGLE_CLIENT_ID,
  isAppleSignInCancelled,
} from '@cleansia/services';
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

  /**
   * Deployment-specific GSI client id, supplied from the app's environment.
   * Empty means Google sign-in is not configured for this deployment.
   */
  private readonly googleClientId = (inject(GOOGLE_CLIENT_ID) ?? '').trim();

  /**
   * Gates the whole Google block. Without a client id GSI answers
   * `403 origin not allowed`, so rendering the button would give the visitor a
   * control that can never sign them in — hide it and keep email/password.
   */
  protected readonly isGoogleSignInConfigured = !!this.googleClientId;

  /**
   * Deployment-specific Apple Services ID (never the iOS bundle id), supplied
   * from the app's environment. Empty means Apple sign-in is not configured.
   */
  private readonly appleClientId = (inject(APPLE_CLIENT_ID) ?? '').trim();

  /**
   * Gates the whole Apple block, same contract as the Google one above: without
   * a Services ID Apple answers `invalid_client`, so the button would be a
   * control that can never sign anyone in.
   */
  protected readonly isAppleSignInConfigured = !!this.appleClientId;

  googleBtnRef = viewChild<ElementRef>('googleBtn');

  ngAfterViewInit() {
    if (!this.isBrowser) return;
    if (this.isGoogleSignInConfigured) this.initGoogleSignIn();
    if (this.isAppleSignInConfigured) void this.initAppleSignIn();
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
      client_id: this.googleClientId,
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

  private _appleRetries = 0;
  private readonly _appleMaxRetries = 20;
  private appleNonce: AppleNonce | null = null;

  private loadAppleIdScript(): void {
    if (document.querySelector('script[src*="appleid.auth.js"]')) return;
    const script = document.createElement('script');
    script.src = APPLE_ID_SCRIPT_URL;
    script.async = true;
    script.defer = true;
    document.head.appendChild(script);
  }

  private async initAppleSignIn(): Promise<void> {
    this.loadAppleIdScript();
    const appleId = getAppleIdApi();
    if (!appleId) {
      if (this._appleRetries < this._appleMaxRetries) {
        this._appleRetries++;
        setTimeout(() => void this.initAppleSignIn(), 300);
      }
      return;
    }

    // The nonce is minted here, ahead of any click: hashing is async and the
    // click handler must reach `signIn()` without awaiting (see below).
    const nonce = await createAppleNonce();
    this.appleNonce = nonce;
    appleId.auth.init({
      clientId: this.appleClientId,
      scope: 'name email',
      redirectURI: window.location.origin + APPLE_REDIRECT_PATH,
      // Apple gets the HASH and echoes it into the token's nonce claim; the raw
      // value below goes to our API, which recomputes the hash to bind the two.
      nonce: nonce.hashed,
      usePopup: true,
    });
  }

  protected signInWithApple(): void {
    const appleId = getAppleIdApi();
    const nonce = this.appleNonce;
    if (!appleId || !nonce) {
      this.facade.appleSignInFailed();
      return;
    }

    // Synchronous from the click handler on purpose — Safari blocks a popup
    // opened after an await, so nothing may be awaited above this line.
    appleId.auth
      .signIn()
      .then((response) => {
        this.zone.run(() =>
          this.facade.appleLogin(
            response.authorization.id_token,
            nonce.raw,
            response.user?.name?.firstName,
            response.user?.name?.lastName
          )
        );
      })
      .catch((error: unknown) => {
        if (!isAppleSignInCancelled(error)) {
          this.zone.run(() => this.facade.appleSignInFailed());
        }
      })
      .finally(() => {
        // Bind the next attempt to its own nonce.
        void this.initAppleSignIn();
      });
  }
}
