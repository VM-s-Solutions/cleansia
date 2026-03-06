import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { JwtToken } from '@cleansia/models';
import {
  ConfirmUserEmailCommand,
  GoogleAuthCommand,
  JwtTokenResponse,
  LoginCommand,
  RegisterCommand,
  RequestPasswordChangeCommand,
  ResendConfirmationEmailCommand,
} from '@cleansia/partner-services';
// Note: LocalStorageKey is available from @cleansia/services if needed
import {
  extractCookieValue,
  removeCookieValue,
  setCookieValue,
  setLocalStorageValueByKey,
} from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { jwtDecode } from 'jwt-decode';
import { BehaviorSubject, Observable, map, of } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';

const CUSTOMER_TOKEN_KEY = 'customer_token';
const CUSTOMER_ROLE_KEY = 'customer_role';

@Injectable({
  providedIn: 'root',
})
export class CustomerAuthService {
  private readonly customerClient = inject(CustomerClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

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
    lastName: string
  ): Observable<boolean> {
    return this.customerClient.authClient.register(
      new RegisterCommand({
        email,
        password,
        firstName,
        lastName,
        language: this.translate.currentLang || this.translate.getDefaultLang(),
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
    this.removeSession();
    this.router.navigate(['login']);
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
    return extractCookieValue(CUSTOMER_TOKEN_KEY);
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
    this.isLoggedIn$.next(true);
  }

  removeSession(): void {
    removeCookieValue(CUSTOMER_TOKEN_KEY);
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
