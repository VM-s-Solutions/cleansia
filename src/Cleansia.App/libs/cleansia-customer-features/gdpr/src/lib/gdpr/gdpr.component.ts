import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { CustomerClient, CustomerAuthService } from '@cleansia/customer-services';
import {
  ConsentType,
  GdprExportDto,
  GrantConsentCommand,
  UserConsentDto,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { FormsModule } from '@angular/forms';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';

@Component({
  selector: 'cleansia-customer-gdpr',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    ToggleSwitchModule,
    ConfirmDialogModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './gdpr.component.html',
  providers: [ConfirmationService],
})
export class GdprComponent implements OnInit {
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly confirmService = inject(ConfirmationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  consents = signal<UserConsentDto[]>([]);
  loadingConsents = signal(true);
  exporting = signal(false);
  deleting = signal(false);

  readonly ConsentType = ConsentType;

  consentLabels: Record<number, string> = {
    [ConsentType.TermsOfService]: 'pages.gdpr.consent_types.terms_of_service',
    [ConsentType.PrivacyPolicy]: 'pages.gdpr.consent_types.privacy_policy',
    [ConsentType.MarketingEmails]: 'pages.gdpr.consent_types.marketing_emails',
    [ConsentType.DataProcessing]: 'pages.gdpr.consent_types.data_processing',
  };

  ngOnInit(): void {
    this.loadConsents();
  }

  loadConsents(): void {
    this.loadingConsents.set(true);
    this.customerClient.gdprClient.consentsGet().subscribe({
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
    this.customerClient.gdprClient
      .consentsPost(new GrantConsentCommand({ consentType, isGranted: granted } as any))
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
    this.customerClient.gdprClient.export().subscribe({
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
    this.confirmService.confirm({
      message: this.translate.instant('pages.gdpr.delete_confirm_message'),
      header: this.translate.instant('pages.gdpr.delete_confirm_title'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('pages.gdpr.delete_confirm_yes'),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => {
        this.deleting.set(true);
        this.customerClient.gdprClient.deleteAccount().subscribe({
          next: () => {
            this.deleting.set(false);
            this.snackbar.showSuccess(
              this.translate.instant('pages.gdpr.delete_success')
            );
            this.authService.logout();
          },
          error: () => {
            this.deleting.set(false);
            this.snackbar.showError(
              this.translate.instant('pages.gdpr.delete_error')
            );
          },
        });
      },
    });
  }
}
