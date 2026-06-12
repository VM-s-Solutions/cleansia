import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  ConsentType,
  ConsentsClient,
  GdprClient,
  GdprExportDto,
  GrantConsentCommand,
  PartnerAuthService,
  UserConsentDto,
  WithdrawConsentCommand,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class PartnerGdprFacade extends UnsubscribeControlDirective {
  private readonly gdprClient = inject(GdprClient);
  private readonly consentsClient = inject(ConsentsClient);
  private readonly authService = inject(PartnerAuthService);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly isAuthenticated = signal<boolean>(this.authService.isLoggedIn());
  readonly consents = signal<UserConsentDto[]>([]);
  readonly loadingConsents = signal<boolean>(false);
  readonly exporting = signal<boolean>(false);
  readonly deleting = signal<boolean>(false);

  loadConsents(): void {
    if (!this.isAuthenticated()) return;

    this.loadingConsents.set(true);
    this.gdprClient
      .consentsGet()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loadingConsents.set(false))
      )
      .subscribe((rows) => {
        if (rows) {
          this.consents.set(rows);
        }
      });
  }

  isConsentGranted(type: ConsentType): boolean {
    const consent = this.consents().find((c) => c.consentType === type);
    return consent?.isGranted ?? false;
  }

  toggleConsent(consentType: ConsentType, granted: boolean): void {
    // IP + user-agent are captured server-side from the request, so the
    // client sends only the consent type.
    const request$ = granted
      ? this.gdprClient.consentsPost(new GrantConsentCommand({ consentType }))
      : this.consentsClient.withdraw(
          new WithdrawConsentCommand({ consentType })
        );

    request$
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showApiError(error, 'pages.gdpr.consent_error');
          return of('error' as const);
        }),
        finalize(() => this.loadConsents())
      )
      .subscribe((result) => {
        if (result !== 'error') {
          this.snackbar.showSuccess(
            this.translate.instant('pages.gdpr.consent_updated')
          );
        }
      });
  }

  exportData(): void {
    if (this.exporting()) return;

    this.exporting.set(true);
    this.gdprClient
      .export()
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showApiError(error, 'pages.gdpr.export_error');
          return of(null);
        }),
        finalize(() => this.exporting.set(false))
      )
      .subscribe((data: GdprExportDto | null) => {
        if (data) {
          this.downloadJson(data, 'my-data-export.json');
          this.snackbar.showSuccess(
            this.translate.instant('pages.gdpr.export_success')
          );
        }
      });
  }

  deleteAccount(): void {
    if (this.deleting()) return;

    this.deleting.set(true);
    this.gdprClient
      .deleteAccount()
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showApiError(error, 'pages.gdpr.delete_error');
          return of('error' as const);
        }),
        finalize(() => this.deleting.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccess(
          this.translate.instant('pages.gdpr.delete_success')
        );
        this.authService
          .logout()
          .pipe(takeUntil(this.destroyed$))
          .subscribe();
      });
  }

  private downloadJson(data: unknown, fileName: string): void {
    if (!this.isBrowser) return;
    const json = JSON.stringify(data, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
