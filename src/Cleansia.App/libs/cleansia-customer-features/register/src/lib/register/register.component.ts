import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, AfterViewInit, computed, ElementRef, viewChild, NgZone, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaCodeInputDialogComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  CodeDialogResult,
} from '@cleansia/components';
import { selectCustomerLoading } from '@cleansia/customer-stores';
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
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { RegisterFacade, ReferralUiState } from './register.facade';
import { checkIfPasswordsValid, PasswordCheck } from './register.models';

/** Map backend ReferralValidationError → i18n key, with a generic fallback. */
const REFERRAL_ERROR_KEYS: Record<string, string> = {
  NotFound: 'auth.register.referral.error_not_found',
  SelfReferral: 'auth.register.referral.error_self_referral',
  AlreadyReferred: 'auth.register.referral.error_already_referred',
  Inactive: 'auth.register.referral.error_inactive',
};

@Component({
  selector: 'cleansia-customer-register',
  templateUrl: './register.component.html',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    ReactiveFormsModule,
    InputTextModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaDynamicBackgroundComponent,
    CleansiaCodeInputDialogComponent,
  ],
  providers: [RegisterFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent implements OnInit, AfterViewInit {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(RegisterFacade);
  protected readonly loading = toSignal(this.store.select(selectCustomerLoading));
  protected routes = CleansiaCustomerRoute;
  private readonly zone = inject(NgZone);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /**
   * Deployment-specific GSI client id, supplied from the app's environment.
   * Empty means Google sign-up is not configured for this deployment.
   */
  private readonly googleClientId = (inject(GOOGLE_CLIENT_ID) ?? '').trim();

  /**
   * Gates the whole Google block. Without a client id GSI answers
   * `403 origin not allowed`, so rendering the button would give the visitor a
   * control that can never register them — hide it and keep the email form.
   */
  protected readonly isGoogleSignInConfigured = !!this.googleClientId;

  /**
   * Deployment-specific Apple Services ID (never the iOS bundle id), supplied
   * from the app's environment. Empty means Apple sign-up is not configured.
   */
  private readonly appleClientId = (inject(APPLE_CLIENT_ID) ?? '').trim();

  /**
   * Gates the whole Apple block, same contract as the Google one above: without
   * a Services ID Apple answers `invalid_client`, so the button would be a
   * control that can never register anyone.
   */
  protected readonly isAppleSignInConfigured = !!this.appleClientId;

  googleBtnRef = viewChild<ElementRef>('googleBtn');

  /** Local-only dialog visibility — facade owns the actual code + state. */
  protected readonly referralDialogVisible = signal(false);

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

  /**
   * Adapt the facade's referral state into the dialog's generic shape, baking
   * in the localized success / error messages. Computed so it reacts to both
   * the facade signal and the active translate language.
   */
  protected readonly referralDialogState = computed<CodeDialogResult>(() => {
    const state = this.facade.referralState();
    return referralStateToDialog(state, this.translate);
  });

  /**
   * Inline note under the referral row for a code that arrived pre-applied
   * (the /r/{code} landing) and failed validation — the visitor never opened
   * the dialog, so the dialog's error message must surface here instead.
   * Purely informative: a bad code never blocks signup.
   */
  protected readonly referralInlineError = computed<string | null>(() => {
    const state = this.facade.referralState();
    if (state.kind !== 'invalid') return null;
    const key =
      REFERRAL_ERROR_KEYS[state.error ?? ''] ?? 'auth.register.referral.error_generic';
    return this.translate.instant(key);
  });

  ngOnInit(): void {
    // Skip on the server — the validate call belongs to the hydrated client,
    // otherwise SSR + hydration would validate the same code twice.
    if (!this.isBrowser) return;
    const code = this.route.snapshot.paramMap.get('code');
    if (code) void this.facade.applyReferralCodeFromUrl(code);
  }

  openReferralDialog(): void {
    this.referralDialogVisible.set(true);
  }

  closeReferralDialog(): void {
    this.referralDialogVisible.set(false);
  }

  /** Bridge dialog `(visibleChange)` into the local signal — types align cleanly. */
  onReferralDialogVisible(visible: boolean): void {
    this.referralDialogVisible.set(visible);
  }

  /**
   * Row clear-X handler. stopPropagation so the click doesn't bubble up to
   * the row's `(click)="openReferralDialog()"` and reopen the dialog we just
   * cleared from.
   */
  clearReferral(event: Event): void {
    event.stopPropagation();
    this.facade.clearReferralCode();
  }

  /** Apply tap handler — single backend call, no debounce. */
  async applyReferral(code: string): Promise<void> {
    await this.facade.validateReferralCodeNow(code);
  }

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
        this.zone.run(() => this.facade.googleRegister(response.credential));
      },
    });

    const btnEl = this.googleBtnRef()?.nativeElement;
    if (btnEl) {
      // GSI's `width` rejects percentage strings — needs an integer pixel
      // value, max 400. Measure the container; fall back to 400 if hidden.
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

  protected signUpWithApple(): void {
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
          this.facade.appleRegister(
            response.authorization.id_token,
            nonce.raw,
            // The one and only moment Apple hands over a name: this is a first
            // authorization, so anything dropped here is dropped permanently.
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

function referralStateToDialog(
  state: ReferralUiState,
  translate: TranslateService,
): CodeDialogResult {
  switch (state.kind) {
    case 'idle':
      return { kind: 'idle' };
    case 'validating':
      return { kind: 'validating' };
    case 'valid': {
      const name = state.referrerFirstName?.trim();
      const messageKey = name
        ? 'auth.register.referral.dialog_success_named'
        : 'auth.register.referral.dialog_success';
      return {
        kind: 'valid',
        successMessage: translate.instant(messageKey, { name: name ?? '' }),
      };
    }
    case 'invalid': {
      const key =
        REFERRAL_ERROR_KEYS[state.error ?? ''] ?? 'auth.register.referral.error_generic';
      return { kind: 'invalid', errorMessage: translate.instant(key) };
    }
  }
}
