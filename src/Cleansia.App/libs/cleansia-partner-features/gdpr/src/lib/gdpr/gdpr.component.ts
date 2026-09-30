import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { PartnerGdprFacade } from './gdpr.facade';

@Component({
  selector: 'cleansia-partner-gdpr',
  standalone: true,
  imports: [
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
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

  ngOnInit(): void {
    this.facade.loadConsents();
  }

  retryConsents(): void {
    this.facade.loadConsents();
  }

  exportData(): void {
    this.facade.exportData();
  }

  deleteAccount(): void {
    this.facade.deleteAccount();
  }
}
