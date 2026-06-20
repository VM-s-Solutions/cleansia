import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS, CommonRoute, LocalStorageKey, Role } from '@cleansia/services';
import {
  getLocalStorageValueByKeyAsJSON,
  setLocalStorageValueByKey,
} from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, Observable, catchError, map, of, tap } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import {
  ConfirmUserEmailCommand,
  GoogleAuthCommand,
  JwtTokenResponse,
  LogoutCommand,
  PartnerLoginCommand,
  RefreshTokenCommand,
  RegisterCommand,
  RegisterEmployeeCommand,
  ResendConfirmationEmailCommand,
  UserProfile,
} from '../client/partner-client';

@Injectable({
  providedIn: 'root',
})
export class PartnerAuthService {
  private readonly partnerClient = inject(PartnerClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly cookieKeys = inject(AUTH_COOKIE_KEYS);

  readonly isLoggedIn$ = new BehaviorSubject<boolean>(this.isLoggedIn());
  readonly isLoggedInAction$: Observable<boolean> = this.isLoggedIn$.pipe(
    map((isLoggedIn: boolean) => {
      if (!isLoggedIn) {
        this.logout().subscribe();
      }
      return isLoggedIn;
    })
  );

  login(
    email: string,
    password: string,
    rememberMe = false
  ): Observable<JwtTokenResponse> {
    return this.partnerClient.authClient.login(
      // trustedDeviceToken stays undefined: the trusted-device lockout bypass
      // reads the raw refresh token server-side from the HttpOnly cookie
      // (cookie wins, never JS-readable) — the browser must not supply it.
      new PartnerLoginCommand({ email, password, rememberMe, trustedDeviceToken: undefined })
    );
  }

  register(
    email: string,
    password: string,
    firstName: string,
    lastName: string,
    referralCode?: string
  ): Observable<boolean> {
    return this.partnerClient.authClient.register(
      new RegisterCommand({
        email,
        password,
        firstName,
        lastName,
        language: this.translate.currentLang || this.translate.getDefaultLang(),
        referralCode,
      })
    );
  }

  registerEmployee(
    email: string,
    password: string,
    firstName: string,
    lastName: string
  ): Observable<boolean> {
    return this.partnerClient.authClient.registerEmployee(
      new RegisterEmployeeCommand({
        email,
        password,
        firstName,
        lastName,
        language: this.translate.currentLang || this.translate.getDefaultLang(),
      })
    );
  }

  confirmUserEmail(code: string): Observable<JwtTokenResponse> {
    return this.partnerClient.authClient
      .confirmUserEmail(new ConfirmUserEmailCommand({ code }))
      .pipe(
        map((authResult: JwtTokenResponse) => {
          this.setSession(authResult);
          return authResult;
        })
      );
  }

  resendEmailConfirmation(email: string): Observable<boolean> {
    return this.partnerClient.authClient
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
    return this.partnerClient.authClient
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

  logout(): Observable<boolean> {
    // Refresh token is in the HttpOnly cookie — server reads from cookie.
    const serverCall = this.partnerClient.authClient
      .logout(new LogoutCommand({ token: '' }))
      .pipe(catchError(() => of(false)));

    return serverCall.pipe(
      tap(() => {
        this.removeSession();
        this.router.navigate([`${CommonRoute.LOGIN}`]);
      }),
      map(() => true)
    );
  }

  refreshSession(): Observable<boolean> {
    // Backend partner controller enriches the command with the host audience
    // and required profile, so we send placeholders here that satisfy the
    // generated TS contract. The refresh token itself is in the cookie.
    return this.partnerClient.authClient
      .refreshToken(
        new RefreshTokenCommand({
          token: '',
          requiredProfile: UserProfile.Employee,
          requiredAudience: undefined,
        })
      )
      .pipe(
        tap((authResult) => this.setSession(authResult)),
        map(() => true)
      );
  }

  isLoggedIn(): boolean {
    // Session is "alive" when we have a CSRF token (proves a session was
    // issued) and the persisted refresh-token exp is in the future.
    if (!this.getCsrfToken()) return false;
    return this.hasValidRefreshToken();
  }

  isLoggedOut(): boolean {
    return !this.isLoggedIn();
  }

  /** CSRF token from the most recent login/refresh response (double-submit pair
   *  with the HttpOnly auth cookie). Read by the interceptor for X-CSRF-Token. */
  getCsrfToken(): string | null {
    return typeof localStorage === 'undefined'
      ? null
      : localStorage.getItem(this.cookieKeys.csrfToken);
  }

  hasValidRefreshToken(): boolean {
    if (typeof localStorage === 'undefined') return false;
    const expStr = localStorage.getItem(this.cookieKeys.refreshTokenExp);
    if (!expStr) return false;
    return Date.now() < new Date(expStr).getTime();
  }

  /** Role attached to the most recent login/refresh response. Source-of-truth
   *  remains server-side; this is a UI hint for permission gating. */
  getRole(): string | null {
    return typeof localStorage === 'undefined'
      ? null
      : localStorage.getItem(this.cookieKeys.role);
  }

  isAdminOrEditor(): boolean {
    if (!this.isLoggedIn()) {
      return false;
    }
    const role = this.getRole();
    return role === Role.ADMINISTRATOR || role === Role.EMPLOYEE;
  }

  setIsWarningShown(isShown: boolean): void {
    this.setWarningDialogStatus(isShown);
  }

  setWarningDialogStatus(isShown: boolean): void {
    setLocalStorageValueByKey(
      LocalStorageKey.WARNING_DIALOG_STATUS,
      isShown.toString()
    );
  }

  getIsWarningShown(): boolean {
    const isShown = getLocalStorageValueByKeyAsJSON(
      LocalStorageKey.WARNING_DIALOG_STATUS
    );
    return isShown ? (isShown as unknown as boolean) : false;
  }

  removeSession(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(this.cookieKeys.refreshTokenExp);
      localStorage.removeItem(this.cookieKeys.csrfToken);
      localStorage.removeItem(this.cookieKeys.role);
    }
    this.isLoggedIn$.next(false);
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

    this.isLoggedIn$.next(true);
    this.setWarningDialogStatus(false);
  }
}
