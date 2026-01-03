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
import { BehaviorSubject, Observable, map, of } from 'rxjs';
import { AdminClient } from '../client/admin-base-client';
import { JwtTokenResponse, LoginCommand } from '../client/admin-client';

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
      new LoginCommand({ email, password, rememberMe })
    );
  }

  logout(): Observable<boolean> {
    this.removeSession();
    this.router.navigate([`${CleansiaAdminRoute.LOGIN}`]);
    return of(true);
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
    this.isLoggedIn$.next(false);
  }

  setSession(authResult: JwtTokenResponse): void {
    this.setCookieSession(authResult.token!);
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
