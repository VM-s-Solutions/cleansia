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
import { DialogService, SnackbarService } from '@cleansia/services';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class PartnerGdprFacade extends UnsubscribeControlDirective {
  private readonly gdprClient = inject(GdprClient);
  private readonly dialog = inject(DialogService);
  private readonly consentsClient = inject(ConsentsClient);
  private readonly authService = inject(PartnerAuthService);
  private readonly snackbar = inject(SnackbarService);
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
    let request$;

    if (granted) {
      const command = new GrantConsentCommand();
      command.consentType = consentType;
      request$ = this.gdprClient.consentsPost(command);
    } else {
      const command = new WithdrawConsentCommand();
      command.consentType = consentType;
      request$ = this.consentsClient.withdraw(command);
    }

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
          this.snackbar.showSuccessTranslated('pages.gdpr.consent_updated');
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
          this.snackbar.showSuccessTranslated('pages.gdpr.export_success');
        }
      });
  }

  deleteAccount(): void {
    if (this.deleting()) return;

    this.dialog
      .confirmTranslated(
        'pages.gdpr.delete_confirm_message',
        'pages.gdpr.delete_confirm_title',
        undefined,
        { danger: true, acceptLabelKey: 'pages.gdpr.delete_confirm_yes' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deleteAccountConfirmed());
  }

  private deleteAccountConfirmed(): void {
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
        // A cleaner is NOT deleted by this call — the backend files a Pending GdprRequest and
        // changes nothing, because ending a working relationship needs signed paperwork and an
        // in-person step. Saying "deleted" and signing them out was a lie the endpoint used to
        // tell truthfully and stopped being able to; they stay signed in and keep working until
        // an admin fulfils the request.
        this.snackbar.showSuccessTranslated('pages.gdpr.delete_requested');
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
