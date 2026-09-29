import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnInit,
  inject,
} from '@angular/core';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { currentLanguage, formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ProfileLegalDocumentsFacade } from '../../profile/profile-legal-documents.facade';

@Component({
  selector: 'cleansia-partner-profile-legal-documents',
  standalone: true,
  imports: [
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
  ],
  templateUrl: './profile-legal-documents.component.html',
  styleUrl: './profile-legal-documents.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileLegalDocumentsComponent implements OnInit {
  @Input({ required: true }) facade!: ProfileLegalDocumentsFacade;

  private readonly lang = currentLanguage(inject(TranslateService));

  ngOnInit(): void {
    this.facade.load();
  }

  /** The effective date is a calendar day, so it is read in UTC like the work contract. */
  effectiveDate(value: Date | string | undefined): string {
    return formatDate(value, this.lang(), 'utcDate');
  }

  acceptedDate(value: Date | string | undefined): string {
    return formatDate(value, this.lang(), 'date');
  }
}
