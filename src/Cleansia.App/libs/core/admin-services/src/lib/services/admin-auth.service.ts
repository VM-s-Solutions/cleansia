/* eslint-disable @typescript-eslint/no-non-null-assertion */
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AUTH_COOKIE_KEYS, CleansiaAdminRoute, LocalStorageKey, Role } from '@cleansia/services';
import {
  getLocalStorageValueByKeyAsJSON,
  setLocalStorageValueByKey,
} from '@cleansia/utils';
import { BehaviorSubject, Observable, catchError, map, of, tap } from 'rxjs';
import { AdminClient } from '../client/admin-base-client';
import {
  AdminLoginCommand,
  JwtTokenResponse,
  LogoutCommand,
  RefreshTokenCommand,
  UserProfile,
} from '../client/admin-client';

@Injectable({
  providedIn: 'root',
})
export class AdminAuthService {
  private readonly adminClient = inject(AdminClient);
  private readonly router = inject(Router);
  private readonly cookieKeys = inject(AUTH_COOKIE_KEYS);

  readonly isLoggedIn$ = new BehaviorSubject<boolean>(this.isLoggedIn());
  readonly isLoggedInAction$: Observable<boolean> = this.isLoggedIn$.pipe(
    map((isLoggedIn: boolean) => {
      if (!isLoggedIn) {
        this.logout();
      }
      return isLoggedIn;
    })
  );

  login(
    email: string,
    password: string,
    rememberMe = false
  ): Observable<JwtTokenResponse> {
    return this.adminClient.adminAuthClient.login(
      new AdminLoginCommand({ email, password, rememberMe })
    );
  }

  logout(): Observable<boolean> {
    // Refresh token is in the HttpOnly cookie — server reads from cookie.
    const serverCall = this.adminClient.adminAuthClient
      .logout(new LogoutCommand({ token: '' }))
      .pipe(catchError(() => of(false)));

    return serverCall.pipe(
      tap(() => {
        this.removeSession();
        this.router.navigate([`${CleansiaAdminRoute.LOGIN}`]);
      }),
      map(() => true)
    );
  }

  refreshSession(): Observable<boolean> {
    return this.adminClient.adminAuthClient
      .refreshToken(
        new RefreshTokenCommand({
          token: '',
          requiredProfile: UserProfile.Administrator,
          requiredAudience: undefined,
        })
      )
      .pipe(
        tap((authResult) => this.setSession(authResult)),
        map(() => true)
      );
  }

  isLoggedIn(): boolean {
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
