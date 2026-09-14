import { ChangeDetectionStrategy, Component } from '@angular/core';
import { LegalDocumentType } from '@cleansia/customer-services';
import { LegalDocumentComponent } from '../legal-document/legal-document.component';

@Component({
  selector: 'cleansia-customer-privacy',
  standalone: true,
  imports: [LegalDocumentComponent],
  templateUrl: './privacy.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyComponent {
  protected readonly LegalDocumentType = LegalDocumentType;
}
