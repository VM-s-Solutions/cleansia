import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, AfterViewInit, computed, ElementRef, viewChild, NgZone, PLATFORM_ID, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaCodeInputDialogComponent,
  CleansiaDynamicBackgroundComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  CodeDialogResult,
} from '@cleansia/components';
import { selectCustomerLoading } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { RegisterFacade, ReferralUiState } from './register.facade';
import { checkIfPasswordsValid, PasswordCheck } from './register.models';

/** Map backend ReferralValidationError → i18n key, with a generic fallback. */
const REFERRAL_ERROR_KEYS: Record<string, string> = {
  NotFound: 'auth.register.referral.error_not_found',
  SelfReferral: 'auth.register.referral.error_self_referral',
  AlreadyReferred: 'auth.register.referral.error_already_referred',
  Inactive: 'auth.register.referral.error_inactive',
};

@Component({
  selector: 'cleansia-customer-register',
  templateUrl: './register.component.html',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    TranslatePipe,
    ReactiveFormsModule,
    InputTextModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaBrandNameComponent,
    CleansiaTextInputComponent,
    CleansiaDynamicBackgroundComponent,
    CleansiaCodeInputDialogComponent,
  ],
  providers: [RegisterFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent implements AfterViewInit {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(RegisterFacade);
  protected readonly loading = toSignal(this.store.select(selectCustomerLoading));
  protected routes = CleansiaCustomerRoute;
  private readonly zone = inject(NgZone);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  googleBtnRef = viewChild<ElementRef>('googleBtn');

  /** Local-only dialog visibility — facade owns the actual code + state. */
  protected readonly referralDialogVisible = signal(false);

  get hasPasswordInput(): boolean {
    return !!this.facade.formGroup.get('password')?.value;
  }

  get hasConfirmPasswordInput(): boolean {
    return !!this.facade.formGroup.get('confirmPassword')?.value;
  }

  get isPasswordValid(): PasswordCheck {
    const password = this.facade.formGroup.get('password')?.value;
    if (!password) return checkIfPasswordsValid('');
    return checkIfPasswordsValid(password);
  }

  get isConfirmPasswordValid(): PasswordCheck {
    const confirmPassword = this.facade.formGroup.get('confirmPassword')?.value;
    if (!confirmPassword) return checkIfPasswordsValid('');
    const password = this.facade.formGroup.get('password')?.value;
    return checkIfPasswordsValid(confirmPassword, password);
  }

  /**
   * Adapt the facade's referral state into the dialog's generic shape, baking
   * in the localized success / error messages. Computed so it reacts to both
   * the facade signal and the active translate language.
   */
  protected readonly referralDialogState = computed<CodeDialogResult>(() => {
    const state = this.facade.referralState();
    return referralStateToDialog(state, this.translate);
  });

  openReferralDialog(): void {
    this.referralDialogVisible.set(true);
  }

  closeReferralDialog(): void {
    this.referralDialogVisible.set(false);
  }

  /** Bridge dialog `(visibleChange)` into the local signal — types align cleanly. */
  onReferralDialogVisible(visible: boolean): void {
    this.referralDialogVisible.set(visible);
  }

  /**
   * Row clear-X handler. stopPropagation so the click doesn't bubble up to
   * the row's `(click)="openReferralDialog()"` and reopen the dialog we just
   * cleared from.
   */
  clearReferral(event: Event): void {
    event.stopPropagation();
    this.facade.clearReferralCode();
  }

  /** Apply tap handler — single backend call, no debounce. */
  async applyReferral(code: string): Promise<void> {
    await this.facade.validateReferralCodeNow(code);
  }

  ngAfterViewInit() {
    if (!this.isBrowser) return;
    this.initGoogleSignIn();
  }

  private _gsiRetries = 0;
  private readonly _gsiMaxRetries = 20;

  private loadGsiScript(): void {
    if (document.querySelector('script[src*="accounts.google.com/gsi/client"]')) return;
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    document.head.appendChild(script);
  }

  private initGoogleSignIn() {
    this.loadGsiScript();
    const google = (window as any).google;
    if (!google?.accounts?.id) {
      if (this._gsiRetries < this._gsiMaxRetries) {
        this._gsiRetries++;
        setTimeout(() => this.initGoogleSignIn(), 300);
      }
      return;
    }

    google.accounts.id.initialize({
      client_id: '354682423254-boe1nlnb1dbd3m6a013d3nkpo2e9bgiq.apps.googleusercontent.com',
      callback: (response: any) => {
        this.zone.run(() => this.facade.googleRegister(response.credential));
      },
    });

    const btnEl = this.googleBtnRef()?.nativeElement;
    if (btnEl) {
      // GSI's `width` rejects percentage strings — needs an integer pixel
      // value, max 400. Measure the container; fall back to 400 if hidden.
      const measured = (btnEl as HTMLElement).clientWidth;
      const width = Math.min(measured > 0 ? measured : 400, 400);
      google.accounts.id.renderButton(btnEl, {
        theme: 'outline',
        size: 'large',
        width,
        text: 'continue_with',
        shape: 'rectangular',
      });
    }
  }
}

function referralStateToDialog(
  state: ReferralUiState,
  translate: TranslateService,
): CodeDialogResult {
  switch (state.kind) {
    case 'idle':
      return { kind: 'idle' };
    case 'validating':
      return { kind: 'validating' };
    case 'valid': {
      const name = state.referrerFirstName?.trim();
      const messageKey = name
        ? 'auth.register.referral.dialog_success_named'
        : 'auth.register.referral.dialog_success';
      return {
        kind: 'valid',
        successMessage: translate.instant(messageKey, { name: name ?? '' }),
      };
    }
    case 'invalid': {
      const key =
        REFERRAL_ERROR_KEYS[state.error ?? ''] ?? 'auth.register.referral.error_generic';
      return { kind: 'invalid', errorMessage: translate.instant(key) };
    }
  }
}
