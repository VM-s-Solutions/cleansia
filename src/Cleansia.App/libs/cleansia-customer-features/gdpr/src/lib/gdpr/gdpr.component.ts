import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { ConsentType } from '@cleansia/customer-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { FormsModule } from '@angular/forms';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { GdprFacade } from './gdpr.facade';

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
  providers: [ConfirmationService, GdprFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GdprComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmationService);
  protected readonly facade = inject(GdprFacade);

  readonly ConsentType = ConsentType;

  consentLabels: Record<number, string> = {
    [ConsentType.TermsOfService]: 'pages.gdpr.consent_types.terms_of_service',
    [ConsentType.PrivacyPolicy]: 'pages.gdpr.consent_types.privacy_policy',
    [ConsentType.MarketingEmails]: 'pages.gdpr.consent_types.marketing_emails',
    [ConsentType.DataProcessing]: 'pages.gdpr.consent_types.data_processing',
  };

  // Re-expose facade signals/methods for template usage without refactoring the markup.
  readonly isAuthenticated = this.facade.isAuthenticated;
  readonly loadingConsents = this.facade.loadingConsents;
  readonly exporting = this.facade.exporting;
  readonly deleting = this.facade.deleting;

  ngOnInit(): void {
    if (this.isAuthenticated()) {
      this.facade.loadConsents();
    } else {
      this.facade.loadingConsents.set(false);
    }
  }

  toggleConsent(consentType: ConsentType, granted: boolean): void {
    this.facade.toggleConsent(consentType, granted);
  }

  isConsentGranted(type: ConsentType): boolean {
    return this.facade.isConsentGranted(type);
  }

  exportData(): void {
    this.facade.exportData();
  }

  deleteAccount(): void {
    this.confirmService.confirm({
      message: this.translate.instant('pages.gdpr.delete_confirm_message'),
      header: this.translate.instant('pages.gdpr.delete_confirm_title'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('pages.gdpr.delete_confirm_yes'),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => {
        this.facade.deleteAccount();
      },
    });
  }
}
