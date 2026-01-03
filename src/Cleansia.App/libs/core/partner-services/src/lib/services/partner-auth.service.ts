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
import { BehaviorSubject, Observable, map, of } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import {
  ConfirmUserEmailCommand,
  GoogleAuthCommand,
  JwtTokenResponse,
  LoginCommand,
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
      new LoginCommand({ email, password, rememberMe })
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
    this.removeSession();
    this.router.navigate([`${CommonRoute.LOGIN}`]);
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
