/* eslint-disable @typescript-eslint/no-non-null-assertion */
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { JwtToken } from '@cleansia/models';
import { CleansiaAdminRoute, LocalStorageKey, Role } from '@cleansia/services';
import {
  extractCookieValue,
  getLocalStorageValueByKeyAsJSON,
  removeCookieValue,
  setCookieValue,
  setLocalStorageValueByKey,
} from '@cleansia/utils';
import { jwtDecode } from 'jwt-decode';
import { BehaviorSubject, Observable, catchError, map, of, tap } from 'rxjs';
import { AdminClient } from '../client/admin-base-client';
import {
  AdminLoginCommand,
  JwtTokenResponse,
  LogoutCommand,
  RefreshTokenCommand,
} from '../client/admin-client';

@Injectable({
  providedIn: 'root',
})
export class AdminAuthService {
  private readonly adminClient = inject(AdminClient);
  private readonly router = inject(Router);

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
    const refreshToken = this.getRefreshToken();
    const serverCall = refreshToken
      ? this.adminClient.adminAuthClient
          .logout(new LogoutCommand({ token: refreshToken }))
          .pipe(catchError(() => of(false)))
      : of(true);

    return serverCall.pipe(
      tap(() => {
        this.removeSession();
        this.router.navigate([`${CleansiaAdminRoute.LOGIN}`]);
      }),
      map(() => true)
    );
  }

  refreshSession(): Observable<string> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }
    return this.adminClient.adminAuthClient
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
    return this.getCookieToken();
  }

  getRefreshToken(): string | null {
    return extractCookieValue(LocalStorageKey.REFRESH_TOKEN);
  }

  hasValidRefreshToken(): boolean {
    if (!this.getRefreshToken()) return false;
    const expStr = localStorage.getItem(LocalStorageKey.REFRESH_TOKEN_EXP);
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
    this.removeCookieSession();
    removeCookieValue(LocalStorageKey.REFRESH_TOKEN);
    localStorage.removeItem(LocalStorageKey.REFRESH_TOKEN_EXP);
    this.isLoggedIn$.next(false);
  }

  setSession(authResult: JwtTokenResponse): void {
    this.setCookieSession(authResult.token!);

    if (authResult.refreshToken) {
      setCookieValue(LocalStorageKey.REFRESH_TOKEN, authResult.refreshToken);
    }
    if (authResult.refreshTokenExpiresAt) {
      localStorage.setItem(
        LocalStorageKey.REFRESH_TOKEN_EXP,
        authResult.refreshTokenExpiresAt.toISOString()
      );
    }

    this.isLoggedIn$.next(true);
    this.setWarningDialogStatus(false);
  }

  private getExpiration(): Date | null {
    return this.getCookieExpiration();
  }

  private removeCookieSession(): void {
    removeCookieValue(LocalStorageKey.TOKEN);
  }

  private getCookieToken(): string | null {
    return extractCookieValue(LocalStorageKey.TOKEN);
  }

  private getCookieExpiration(): Date | null {
    const token = this.getCookieToken();
    if (!token) {
      return null;
    }

    const { exp } = jwtDecode(token);
    return exp ? new Date(exp * 1000) : null;
  }

  private setCookieSession(token: string): void {
    const decodedToken: JwtToken = jwtDecode(token);
    setCookieValue(LocalStorageKey.TOKEN, token);
    setLocalStorageValueByKey(LocalStorageKey.ROLE, decodedToken.role);
  }
}
