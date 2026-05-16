import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import {
  ConsentType,
  GdprExportDto,
  GrantConsentCommand,
  UserConsentDto,
  WithdrawConsentCommand,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class GdprFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly consents = signal<UserConsentDto[]>([]);
  readonly loadingConsents = signal(true);
  readonly exporting = signal(false);
  readonly deleting = signal(false);

  readonly isAuthenticated = this.authService.isLoggedIn;

  loadConsents(): void {
    this.loadingConsents.set(true);
    this.customerClient.gdprClient
      .consentsGet()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (consents) => {
          this.consents.set(consents);
          this.loadingConsents.set(false);
        },
        error: () => {
          this.loadingConsents.set(false);
        },
      });
  }

  toggleConsent(consentType: ConsentType, granted: boolean): void {
    // Backend now exposes Grant and Withdraw as separate endpoints. IP +
    // user-agent are populated server-side from the request (legal-audit
    // integrity), so we don't pass them here.
    const request$ = granted
      ? this.customerClient.gdprClient.consentsPost(
          new GrantConsentCommand({ consentType })
        )
      : this.customerClient.consentsClient.withdraw(
          new WithdrawConsentCommand({ consentType })
        );

    request$
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.snackbar.showSuccess(
            this.translate.instant('pages.gdpr.consent_updated')
          );
          this.loadConsents();
        },
        error: () => {
          this.snackbar.showError(
            this.translate.instant('pages.gdpr.consent_error')
          );
          this.loadConsents();
        },
      });
  }

  isConsentGranted(type: ConsentType): boolean {
    const consent = this.consents().find((c) => c.consentType === type);
    return consent?.isGranted ?? false;
  }

  exportData(): void {
    this.exporting.set(true);
    this.customerClient.gdprClient
      .export()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (data: GdprExportDto) => {
          const json = JSON.stringify(data, null, 2);
          const blob = new Blob([json], { type: 'application/json' });
          if (this.isBrowser) {
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'my-data-export.json';
            a.click();
            URL.revokeObjectURL(url);
          }
          this.exporting.set(false);
          this.snackbar.showSuccess(
            this.translate.instant('pages.gdpr.export_success')
          );
        },
        error: () => {
          this.exporting.set(false);
          this.snackbar.showError(
            this.translate.instant('pages.gdpr.export_error')
          );
        },
      });
  }

  deleteAccount(): void {
    this.deleting.set(true);
    this.customerClient.gdprClient
      .deleteAccount()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          this.snackbar.showSuccess(
            this.translate.instant('pages.gdpr.delete_success')
          );
          // Cold Observable — must subscribe so the local cleanup + redirect run.
          this.authService.logout().pipe(takeUntil(this.destroyed$)).subscribe();
        },
        error: () => {
          this.deleting.set(false);
          this.snackbar.showError(
            this.translate.instant('pages.gdpr.delete_error')
          );
        },
      });
  }
}
