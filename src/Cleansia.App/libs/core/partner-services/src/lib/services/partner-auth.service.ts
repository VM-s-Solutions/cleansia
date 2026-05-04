import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { JwtToken } from '@cleansia/models';
import { CommonRoute, LocalStorageKey, Role } from '@cleansia/services';
import {
  extractCookieValue,
  getLocalStorageValueByKeyAsJSON,
  removeCookieValue,
  setCookieValue,
  setLocalStorageValueByKey,
} from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { jwtDecode } from 'jwt-decode';
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
} from '../client/partner-client';

@Injectable({
  providedIn: 'root',
})
export class PartnerAuthService {
  private readonly partnerClient = inject(PartnerClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

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
    return this.partnerClient.authClient.login(
      new PartnerLoginCommand({ email, password, rememberMe })
    );
  }

  register(
    email: string,
    password: string,
    firstName: string,
    lastName: string
  ): Observable<boolean> {
    return this.partnerClient.authClient.register(
      new RegisterCommand({
        email,
        password,
        firstName,
        lastName,
        language: this.translate.currentLang || this.translate.getDefaultLang(),
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
    const refreshToken = this.getRefreshToken();
    const serverCall = refreshToken
      ? this.partnerClient.authClient
          .logout(new LogoutCommand({ token: refreshToken }))
          .pipe(catchError(() => of(false)))
      : of(true);

    return serverCall.pipe(
      tap(() => {
        this.removeSession();
        this.router.navigate([`${CommonRoute.LOGIN}`]);
      }),
      map(() => true)
    );
  }

  refreshSession(): Observable<string> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }
    return this.partnerClient.authClient
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

  /**
   * Replaces the current JWT token with a new one (e.g., after profile upgrade).
   */
  updateToken(token: string): void {
    this.setCookieSession(token);
  }

  private setCookieSession(token: string): void {
    const decodedToken: JwtToken = jwtDecode(token);
    setCookieValue(LocalStorageKey.TOKEN, token);
    setLocalStorageValueByKey(LocalStorageKey.ROLE, decodedToken.role);
  }
}
