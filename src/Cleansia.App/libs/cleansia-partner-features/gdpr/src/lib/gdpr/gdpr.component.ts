import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { ConsentType } from '@cleansia/partner-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { PartnerGdprFacade } from './gdpr.facade';

@Component({
  selector: 'cleansia-partner-gdpr',
  standalone: true,
  imports: [
    FormsModule,
    TranslatePipe,
    ToggleSwitchModule,
    ConfirmDialogModule,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaTitleComponent,
    RouterModule,
  ],
  templateUrl: './gdpr.component.html',
  styleUrl: './gdpr.component.scss',
  providers: [PartnerGdprFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartnerGdprComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);
  protected readonly facade = inject(PartnerGdprFacade);

  readonly ConsentType = ConsentType;

  readonly consentLabels: Readonly<Record<ConsentType, string>> = {
    [ConsentType.TermsOfService]: 'pages.gdpr.consent_types.terms_of_service',
    [ConsentType.PrivacyPolicy]: 'pages.gdpr.consent_types.privacy_policy',
    [ConsentType.MarketingEmails]: 'pages.gdpr.consent_types.marketing_emails',
    [ConsentType.DataProcessing]: 'pages.gdpr.consent_types.data_processing',
  };

  readonly consentTypes: readonly ConsentType[] = [
    ConsentType.TermsOfService,
    ConsentType.PrivacyPolicy,
    ConsentType.MarketingEmails,
    ConsentType.DataProcessing,
  ];

  ngOnInit(): void {
    this.facade.loadConsents();
  }

  toggleConsent(consentType: ConsentType, granted: boolean): void {
    this.facade.toggleConsent(consentType, granted);
  }

  exportData(): void {
    this.facade.exportData();
  }

  deleteAccount(): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.gdpr.delete_confirm_message'),
      header: this.translate.instant('pages.gdpr.delete_confirm_title'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('pages.gdpr.delete_confirm_yes'),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => this.facade.deleteAccount(),
    });
  }
}
