import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { JwtToken } from '@cleansia/models';
import {
  ConfirmUserEmailCommand,
  GoogleAuthCommand,
  JwtTokenResponse,
  RequestPasswordChangeCommand,
  ResendConfirmationEmailCommand,
} from '@cleansia/partner-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import {
  LoginCommand,
  LogoutCommand,
  RefreshTokenCommand,
  RegisterCommand,
} from '../client/customer-client';
// Note: LocalStorageKey is available from @cleansia/services if needed
import {
  extractCookieValue,
  removeCookieValue,
  setCookieValue,
  setLocalStorageValueByKey,
} from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { jwtDecode } from 'jwt-decode';
import { BehaviorSubject, Observable, catchError, map, of, tap } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';

const CUSTOMER_TOKEN_KEY = 'customer_token';
const CUSTOMER_REFRESH_TOKEN_KEY = 'customer_refresh_token';
const CUSTOMER_REFRESH_EXP_KEY = 'customer_refresh_exp'; // localStorage, ISO string
const CUSTOMER_ROLE_KEY = 'customer_role';

@Injectable({
  providedIn: 'root',
})
export class CustomerAuthService {
  private readonly customerClient = inject(CustomerClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly savedAddressStore = inject(SavedAddressStore);

  readonly isLoggedIn$ = new BehaviorSubject<boolean>(this.isLoggedIn());

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
    const refreshToken = this.getRefreshToken();
    // Best-effort call to backend to revoke the refresh token. If it fails
    // (offline, network error, etc.) we still wipe local state — user intent is clear.
    const serverCall = refreshToken
      ? this.customerClient.authClient
          .logout(new LogoutCommand({ token: refreshToken }))
          .pipe(catchError(() => of(false)))
      : of(true);

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
   * Exchanges the stored refresh token for a new access+refresh pair. Called by
   * the error interceptor on 401. Emits the new access token on success; errors
   * cause the interceptor to fall through to full logout.
   */
  refreshSession(): Observable<string> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }
    return this.customerClient.authClient
      .refreshToken(new RefreshTokenCommand({ token: refreshToken }))
      .pipe(
        tap((authResult) => this.setSession(authResult)),
        map((authResult) => authResult.token!)
      );
  }

  isLoggedIn(): boolean {
    const expiration = this.getExpiration();
    return expiration ? Date.now() < expiration.getTime() : false;
  }

  isLoggedOut(): boolean {
    return !this.isLoggedIn();
  }

  getToken(): string | null {
    return extractCookieValue(CUSTOMER_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return extractCookieValue(CUSTOMER_REFRESH_TOKEN_KEY);
  }

  /** True if we have a refresh token and it hasn't expired yet. */
  hasValidRefreshToken(): boolean {
    if (!this.getRefreshToken()) return false;
    const expStr = localStorage.getItem(CUSTOMER_REFRESH_EXP_KEY);
    if (!expStr) return false;
    return Date.now() < new Date(expStr).getTime();
  }

  getRole(): string | null {
    const token: string | null = this.getToken();
    if (!token) {
      return null;
    }
    const decodedToken: JwtToken = jwtDecode(token);
    return decodedToken.role;
  }

  setSession(authResult: JwtTokenResponse): void {
    const token = authResult.token!;
    const decodedToken: JwtToken = jwtDecode(token);
    setCookieValue(CUSTOMER_TOKEN_KEY, token);
    setLocalStorageValueByKey(CUSTOMER_ROLE_KEY, decodedToken.role);

    if (authResult.refreshToken) {
      setCookieValue(CUSTOMER_REFRESH_TOKEN_KEY, authResult.refreshToken);
    }
    if (authResult.refreshTokenExpiresAt) {
      localStorage.setItem(
        CUSTOMER_REFRESH_EXP_KEY,
        authResult.refreshTokenExpiresAt.toISOString()
      );
    }

    this.isLoggedIn$.next(true);

    // Preload saved addresses so the order wizard finds them warm, even when
    // the user lands there without visiting profile first. Fire-and-forget —
    // refresh() already snackbars on failure and must not block sign-in.
    void this.savedAddressStore.refresh();
  }

  removeSession(): void {
    removeCookieValue(CUSTOMER_TOKEN_KEY);
    removeCookieValue(CUSTOMER_REFRESH_TOKEN_KEY);
    localStorage.removeItem(CUSTOMER_REFRESH_EXP_KEY);
    // Blank the cached addresses so user B doesn't see user A's list on the
    // same device between sign-out and the next post-signin refresh().
    this.savedAddressStore.clear();
    this.isLoggedIn$.next(false);
  }

  private getExpiration(): Date | null {
    const token = this.getToken();
    if (!token) {
      return null;
    }
    const { exp } = jwtDecode(token);
    return exp ? new Date(exp * 1000) : null;
  }
}
