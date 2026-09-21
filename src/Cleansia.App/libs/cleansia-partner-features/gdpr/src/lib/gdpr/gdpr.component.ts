import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { ConsentType } from '@cleansia/partner-services';
import { TranslatePipe } from '@ngx-translate/core';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { PartnerGdprFacade } from './gdpr.facade';

@Component({
  selector: 'cleansia-partner-gdpr',
  standalone: true,
  imports: [
    FormsModule,
    TranslatePipe,
    ToggleSwitchModule,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaTitleComponent,
    RouterModule,
  ],
  templateUrl: './gdpr.component.html',
  styleUrl: './gdpr.component.scss',
  providers: [PartnerGdprFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartnerGdprComponent implements OnInit {
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
    this.facade.deleteAccount();
  }
}
