import { isPlatformBrowser } from '@angular/common';
import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  ConsentType,
  CustomerAuthService,
  CustomerClient,
  GdprExportDto,
  NotificationPreferencesDto,
  UpdateNotificationPreferencesCommand,
  UserConsentDto,
} from '@cleansia/customer-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { finalize, forkJoin, takeUntil } from 'rxjs';

const LEGAL_CONSENTS = [
  {
    type: ConsentType.TermsOfService,
    labelKey: 'pages.gdpr.consent_types.terms_of_service',
  },
  {
    type: ConsentType.PrivacyPolicy,
    labelKey: 'pages.gdpr.consent_types.privacy_policy',
  },
];

@Injectable()
export class GdprFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly dialog = inject(DialogService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly language = signal<string>(this.translate.currentLang);

  readonly consents = signal<UserConsentDto[]>([]);
  readonly preferences = signal<NotificationPreferencesDto | null>(null);
  readonly loadingConsents = signal(true);
  readonly consentsError = signal(false);
  readonly savingMarketing = signal(false);
  readonly exporting = signal(false);
  readonly deleting = signal(false);

  readonly isAuthenticated = this.authService.isLoggedIn;

  readonly legalConsents = computed(() => {
    const lang = this.language();
    return LEGAL_CONSENTS.map(({ type, labelKey }) => {
      const consent = this.consents().find((c) => c.consentType === type);
      const accepted = consent?.isGranted === true;
      const version = accepted ? consent.documentVersion ?? '' : '';
      return {
        type,
        labelKey,
        detailKey: !accepted
          ? 'pages.gdpr.legal.not_accepted'
          : version
            ? 'pages.gdpr.legal.accepted_version'
            : 'pages.gdpr.legal.accepted',
        detailParams: {
          version,
          date: accepted ? formatDate(consent.grantedAt, lang) : '',
        },
        newerVersionInForce: accepted && !consent.coversCurrentVersion,
      };
    });
  });

  readonly marketingConsent = computed(() => this.preferences()?.promo ?? false);

  constructor() {
    super();
    this.translate.onLangChange
      .pipe(takeUntil(this.destroyed$))
      .subscribe(({ lang }) => this.language.set(lang));
  }

  loadConsents(): void {
    this.loadingConsents.set(true);
    this.consentsError.set(false);
    forkJoin([
      this.customerClient.gdprClient.consentsGet(),
      this.customerClient.notificationPreferencesClient.getMine(),
    ])
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: ([consents, preferences]) => {
          // The generated client answers a 200 whose body is not a JSON array, and a 204, with
          // NULL while its declared type promises an array.
          this.consents.set(consents ?? []);
          this.preferences.set(preferences);
          this.loadingConsents.set(false);
        },
        error: () => {
          this.consentsError.set(true);
          this.loadingConsents.set(false);
        },
      });
  }

  setMarketingConsent(granted: boolean): void {
    const current = this.preferences();
    if (!current || this.savingMarketing()) return;

    const changed = { ...current.toJSON(), promo: granted };
    // Shown before the save so that restoring `current` on a refusal is a change the switch sees.
    this.preferences.set(NotificationPreferencesDto.fromJS(changed));
    this.savingMarketing.set(true);
    this.customerClient.notificationPreferencesClient
      .update(UpdateNotificationPreferencesCommand.fromJS(changed))
      .pipe(
        takeUntil(this.destroyed$),
        finalize(() => this.savingMarketing.set(false))
      )
      .subscribe({
        next: (saved) => {
          if (saved) this.preferences.set(saved);
          this.snackbar.showSuccess(
            this.translate.instant('pages.gdpr.consent_updated')
          );
        },
        error: () => {
          this.preferences.set(current);
          this.snackbar.showError(
            this.translate.instant('pages.gdpr.consent_error')
          );
        },
      });
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

  /** Asks on the app shell's confirm dialog, and deletes the account only on yes. */
  confirmDeleteAccount(): void {
    this.dialog
      .confirmTranslated('pages.gdpr.delete_confirm_message', 'pages.gdpr.delete_confirm_title', undefined, {
        acceptLabelKey: 'pages.gdpr.delete_confirm_yes',
      })
      .pipe(takeUntil(this.destroyed$))
      .subscribe((confirmed) => {
        if (confirmed) this.deleteAccount();
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
