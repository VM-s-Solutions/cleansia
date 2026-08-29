import { Injectable, PLATFORM_ID, Signal, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS, CleansiaCustomerRoute } from '@cleansia/services';
import {
  AppleAuthCommand,
  ConfirmUserEmailCommand,
  GoogleAuthCommand,
  JwtTokenResponse,
  LoginCommand,
  LogoutCommand,
  RefreshTokenCommand,
  RegisterCommand,
  RequestPasswordChangeCommand,
  ResendConfirmationEmailCommand,
} from '../client/customer-client';
import { setLocalStorageValueByKey } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { SESSION_LIFECYCLE_LISTENERS } from './session-lifecycle';
import { SignupConsentService } from './signup-consent.service';

@Injectable({
  providedIn: 'root',
})
export class CustomerAuthService {
  private readonly customerClient = inject(CustomerClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly sessionListeners =
    inject(SESSION_LIFECYCLE_LISTENERS, { optional: true }) ?? [];
  private readonly signupConsent = inject(SignupConsentService);
  private readonly cookieKeys = inject(AUTH_COOKIE_KEYS);
  // Guard storage access by platform, not `typeof localStorage` — Node 22+
  // exposes a global localStorage whose methods throw during SSR.
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // Reactive session flag. Auth tokens are HttpOnly cookies — JS can't
  // observe them, so we track session existence via the CSRF token (which
  // is only set when the server issued a session) + refreshTokenExp.
  private readonly _isLoggedIn = signal<boolean>(this.hasValidSession());
  readonly isLoggedIn: Signal<boolean> = computed(() => this._isLoggedIn());

  private currentLanguage(): string {
    return this.translate.currentLang || this.translate.getDefaultLang();
  }

  login(
    email: string,
    password: string,
    rememberMe = false
  ): Observable<JwtTokenResponse> {
    const command = new LoginCommand();
    command.email = email;
    command.password = password;
    command.rememberMe = rememberMe;

    return this.customerClient.authClient.login(command);
  }

  register(
    email: string,
    password: string,
    firstName: string,
    lastName: string,
    referralCode?: string
  ): Observable<boolean> {
    const command = new RegisterCommand();
    command.email = email;
    command.password = password;
    command.firstName = firstName;
    command.lastName = lastName;
    command.language = this.currentLanguage();
    command.referralCode = referralCode?.trim()
      ? referralCode.trim().toUpperCase()
      : undefined;

    // 200 with no body since T-0665 — the bool was always `true`, failures come through as
    // errors. Success is "it did not throw", matching logout() in this same service.
    return this.customerClient.authClient.register(command).pipe(map(() => true));
  }

  confirmUserEmail(code: string, email: string): Observable<JwtTokenResponse> {
    const command = new ConfirmUserEmailCommand();
    command.code = code;
    command.email = email;

    return this.customerClient.authClient.confirmUserEmail(command).pipe(
      map((authResult: JwtTokenResponse) => {
        this.setSession(authResult);
        return authResult;
      })
    );
  }

  resendEmailConfirmation(email: string): Observable<boolean> {
    const command = new ResendConfirmationEmailCommand();
    command.email = email;
    command.language = this.currentLanguage();

    return this.customerClient.authClient
      .resendConfirmationEmail(command)
      .pipe(map(() => true));
  }

  /**
   * Signup and sign-in are four methods rather than two with a flag because the
   * consent assertion has to be a property of the call. One root-provided
   * service serves both screens, so anything the flag could be read from
   * outlives the call that set it — and a ticked signup form reaching a sign-in
   * request provisions an account nobody agreed to. An identity the sign-in
   * pair does not recognize is refused with `auth.social_account_not_found`.
   */
  signUpWithGoogle(
    token: string,
    googleId: string,
    email: string,
    firstName: string,
    lastName: string
  ): Observable<JwtTokenResponse> {
    return this.googleAuth(token, googleId, email, firstName, lastName, true);
  }

  signInWithGoogle(
    token: string,
    googleId: string,
    email: string,
    firstName: string,
    lastName: string
  ): Observable<JwtTokenResponse> {
    return this.googleAuth(token, googleId, email, firstName, lastName, false);
  }

  signUpWithApple(
    identityToken: string,
    rawNonce: string,
    firstName?: string,
    lastName?: string
  ): Observable<JwtTokenResponse> {
    return this.appleAuth(identityToken, rawNonce, firstName, lastName, true);
  }

  signInWithApple(
    identityToken: string,
    rawNonce: string,
    firstName?: string,
    lastName?: string
  ): Observable<JwtTokenResponse> {
    return this.appleAuth(identityToken, rawNonce, firstName, lastName, false);
  }

  private googleAuth(
    token: string,
    googleId: string,
    email: string,
    firstName: string,
    lastName: string,
    termsAccepted: boolean
  ): Observable<JwtTokenResponse> {
    const command = new GoogleAuthCommand();
    command.token = token;
    command.googleId = googleId;
    command.email = email;
    command.firstName = firstName;
    command.lastName = lastName;
    command.termsAccepted = termsAccepted;

    return this.customerClient.authClient.googleAuth(command).pipe(
      map((authResult: JwtTokenResponse) => {
        this.setSession(authResult);
        return authResult;
      })
    );
  }

  /**
   * `rawNonce` is the RAW nonce; Apple was handed its SHA-256. See
   * `createAppleNonce`.
   */
  private appleAuth(
    identityToken: string,
    rawNonce: string,
    firstName: string | undefined,
    lastName: string | undefined,
    termsAccepted: boolean
  ): Observable<JwtTokenResponse> {
    const command = new AppleAuthCommand();
    command.identityToken = identityToken;
    command.rawNonce = rawNonce;
    command.firstName = firstName;
    command.lastName = lastName;
    command.termsAccepted = termsAccepted;

    return this.customerClient.authClient.appleAuth(command).pipe(
      map((authResult: JwtTokenResponse) => {
        this.setSession(authResult);
        return authResult;
      })
    );
  }

  forgotPassword(email: string): Observable<boolean> {
    const command = new RequestPasswordChangeCommand();
    command.email = email;
    command.language = this.currentLanguage();

    return this.customerClient.userClient
      .requestPasswordChange(command)
      .pipe(map(() => true));
  }

  logout(): Observable<boolean> {
    // Refresh token lives in the HttpOnly cookie — the server reads it from
    // there. We still POST so the server can revoke it; best-effort: if the
    // call fails (offline, etc.) we wipe local state anyway because user
    // intent is clear.
    const command = new LogoutCommand();
    command.token = '';

    const serverCall = this.customerClient.authClient
      .logout(command)
      .pipe(catchError(() => of(false)));

    return serverCall.pipe(
      tap(() => {
        this.removeSession();
        // Absolute path so logout from any nested feature route (orders/*,
        // membership/*, etc.) lands on /login instead of resolving relatively
        // to a child path like /orders/login.
        this.router.navigate(['/' + CleansiaCustomerRoute.LOGIN]);
      }),
      map(() => true)
    );
  }

  /**
   * Exchanges the cookie-carried refresh token for a new access+refresh pair
   * (the server rotates both cookies). Called by the error interceptor on 401.
   * Resolves to true on success; errors propagate so the interceptor can fall
   * through to full logout.
   */
  refreshSession(): Observable<boolean> {
    const command = new RefreshTokenCommand();
    command.token = '';

    return this.customerClient.authClient.refreshToken(command).pipe(
      tap((authResult) => this.setSession(authResult)),
      map(() => true)
    );
  }

  /**
   * Snapshot check — derived from the persisted refresh-token expiry +
   * presence of a CSRF token (the latter is only set when the server
   * issued a session). Use this for SSR-safe / startup checks; for
   * reactive gating prefer the `isLoggedIn` signal.
   */
  hasValidSession(): boolean {
    if (!this.getCsrfToken()) return false;
    return this.hasValidRefreshToken();
  }

  isLoggedOut(): boolean {
    return !this.isLoggedIn();
  }

  /**
   * CSRF token from the most recent login/refresh response. Sent as the
   * `X-CSRF-Token` header by the auth interceptor on state-changing
   * requests. Stored in localStorage (JS-readable on purpose — it's the
   * client half of the double-submit pair; the matching value lives in
   * the HttpOnly auth cookie's signature).
   */
  getCsrfToken(): string | null {
    return this.isBrowser
      ? localStorage.getItem(this.cookieKeys.csrfToken)
      : null;
  }

  /** True if the server-issued refresh token hasn't expired yet (per the
   *  exp we persisted from the login response). */
  hasValidRefreshToken(): boolean {
    if (!this.isBrowser) return false;
    const expStr = localStorage.getItem(this.cookieKeys.refreshTokenExp);
    if (!expStr) return false;
    return Date.now() < new Date(expStr).getTime();
  }

  /** Returns the role the server attached to the most recent login/refresh
   *  response. Source-of-truth for permission decisions stays server-side;
   *  this is a UI hint only. */
  getRole(): string | null {
    return this.isBrowser
      ? localStorage.getItem(this.cookieKeys.role)
      : null;
  }

  setSession(authResult: JwtTokenResponse): void {
    // Auth + refresh tokens land as HttpOnly cookies via Set-Cookie; we only
    // persist the JS-readable companions (role, csrf, refresh exp).
    const role = (authResult as unknown as { role?: string }).role;
    if (role) {
      setLocalStorageValueByKey(this.cookieKeys.role, role);
    }
    if (authResult.refreshTokenExpiresAt) {
      localStorage.setItem(
        this.cookieKeys.refreshTokenExp,
        authResult.refreshTokenExpiresAt.toISOString()
      );
    }
    if (authResult.csrfToken) {
      localStorage.setItem(this.cookieKeys.csrfToken, authResult.csrfToken);
    }

    this._isLoggedIn.set(true);

    // The signup tick predates any session, and the identity here is the
    // server's rather than whatever a form held.
    this.signupConsent.flush(authResult.email);

    // Preload saved addresses so the order wizard finds them warm, even when
    // the user lands there without visiting profile first. Fire-and-forget —
    // refresh() already snackbars on failure and must not block sign-in.
    this.sessionListeners.forEach((listener) => listener.onSessionStarted());
  }

  removeSession(): void {
    if (this.isBrowser) {
      localStorage.removeItem(this.cookieKeys.refreshTokenExp);
      localStorage.removeItem(this.cookieKeys.csrfToken);
      localStorage.removeItem(this.cookieKeys.role);
    }
    // The HttpOnly access + refresh cookies are cleared server-side by the
    // Logout endpoint's Set-Cookie deletes — JS can't touch them directly.
    // Blank the cached addresses so user B doesn't see user A's list on the
    // same device between sign-out and the next post-signin refresh().
    this.sessionListeners.forEach((listener) => listener.onSessionEnded());
    this._isLoggedIn.set(false);
  }
}
