import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SavedAddressStore } from '@cleansia/customer-stores';
import {
  ConfirmUserEmailCommand,
  GoogleAuthCommand,
  JwtTokenResponse,
  RequestPasswordChangeCommand,
  ResendConfirmationEmailCommand,
} from '@cleansia/partner-services';
import { AUTH_COOKIE_KEYS, CleansiaCustomerRoute } from '@cleansia/services';
import {
  LoginCommand,
  LogoutCommand,
  RefreshTokenCommand,
  RegisterCommand,
} from '../client/customer-client';
import { setLocalStorageValueByKey } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';

@Injectable({
  providedIn: 'root',
})
export class CustomerAuthService {
  private readonly customerClient = inject(CustomerClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly savedAddressStore = inject(SavedAddressStore);
  private readonly cookieKeys = inject(AUTH_COOKIE_KEYS);

  // Reactive session flag. Auth tokens are HttpOnly cookies — JS can't
  // observe them, so we track session existence via the CSRF token (which
  // is only set when the server issued a session) + refreshTokenExp.
  private readonly _isLoggedIn = signal<boolean>(this.hasValidSession());
  readonly isLoggedIn: Signal<boolean> = computed(() => this._isLoggedIn());

  login(
    email: string,
    password: string,
    rememberMe = false
  ): Observable<JwtTokenResponse> {
    return this.customerClient.authClient.login(
      new LoginCommand({ email, password, rememberMe })
    );
  }

  register(
    email: string,
    password: string,
    firstName: string,
    lastName: string,
    referralCode?: string
  ): Observable<boolean> {
    return this.customerClient.authClient.register(
      new RegisterCommand({
        email,
        password,
        firstName,
        lastName,
        language: this.translate.currentLang || this.translate.getDefaultLang(),
        referralCode: referralCode?.trim() ? referralCode.trim().toUpperCase() : undefined,
      })
    );
  }

  confirmUserEmail(code: string): Observable<JwtTokenResponse> {
    return this.customerClient.authClient
      .confirmUserEmail(new ConfirmUserEmailCommand({ code }))
      .pipe(
        map((authResult: JwtTokenResponse) => {
          this.setSession(authResult);
          return authResult;
        })
      );
  }

  resendEmailConfirmation(email: string): Observable<boolean> {
    return this.customerClient.authClient
      .resendConfirmationEmail(
        new ResendConfirmationEmailCommand({
          email,
          language:
            this.translate.currentLang || this.translate.getDefaultLang(),
        })
      )
      .pipe(map(() => true));
  }

  authenticateWithGoogle(
    token: string,
    googleId: string,
    email: string,
    firstName: string,
    lastName: string
  ): Observable<JwtTokenResponse> {
    return this.customerClient.authClient
      .googleAuth(
        new GoogleAuthCommand({ token, googleId, email, firstName, lastName })
      )
      .pipe(
        map((authResult: JwtTokenResponse) => {
          this.setSession(authResult);
          return authResult;
        })
      );
  }

  forgotPassword(email: string): Observable<boolean> {
    return this.customerClient.userClient
      .requestPasswordChange(
        new RequestPasswordChangeCommand({
          email,
          language:
            this.translate.currentLang || this.translate.getDefaultLang(),
        })
      )
      .pipe(map(() => true));
  }

  logout(): Observable<boolean> {
    // Refresh token lives in the HttpOnly cookie — the server reads it from
    // there. We still POST so the server can revoke it; best-effort: if the
    // call fails (offline, etc.) we wipe local state anyway because user
    // intent is clear.
    const serverCall = this.customerClient.authClient
      .logout(new LogoutCommand({ token: '' }))
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
    return this.customerClient.authClient
      .refreshToken(new RefreshTokenCommand({ token: '' }))
      .pipe(
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
    return typeof localStorage === 'undefined'
      ? null
      : localStorage.getItem(this.cookieKeys.csrfToken);
  }

  /** True if the server-issued refresh token hasn't expired yet (per the
   *  exp we persisted from the login response). */
  hasValidRefreshToken(): boolean {
    if (typeof localStorage === 'undefined') return false;
    const expStr = localStorage.getItem(this.cookieKeys.refreshTokenExp);
    if (!expStr) return false;
    return Date.now() < new Date(expStr).getTime();
  }

  /** Returns the role the server attached to the most recent login/refresh
   *  response. Source-of-truth for permission decisions stays server-side;
   *  this is a UI hint only. */
  getRole(): string | null {
    return typeof localStorage === 'undefined'
      ? null
      : localStorage.getItem(this.cookieKeys.role);
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

    // Preload saved addresses so the order wizard finds them warm, even when
    // the user lands there without visiting profile first. Fire-and-forget —
    // refresh() already snackbars on failure and must not block sign-in.
    void this.savedAddressStore.refresh();
  }

  removeSession(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(this.cookieKeys.refreshTokenExp);
      localStorage.removeItem(this.cookieKeys.csrfToken);
      localStorage.removeItem(this.cookieKeys.role);
    }
    // The HttpOnly access + refresh cookies are cleared server-side by the
    // Logout endpoint's Set-Cookie deletes — JS can't touch them directly.
    // Blank the cached addresses so user B doesn't see user A's list on the
    // same device between sign-out and the next post-signin refresh().
    this.savedAddressStore.clear();
    this._isLoggedIn.set(false);
  }
}
