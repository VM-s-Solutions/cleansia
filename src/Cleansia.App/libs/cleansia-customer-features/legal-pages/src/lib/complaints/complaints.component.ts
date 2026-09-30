import { ChangeDetectionStrategy, Component } from '@angular/core';
import { LegalDocumentType } from '@cleansia/customer-services';
import { LegalDocumentComponent } from '../legal-document/legal-document.component';

@Component({
  selector: 'cleansia-customer-complaints',
  standalone: true,
  imports: [LegalDocumentComponent],
  templateUrl: './complaints.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComplaintsComponent {
  protected readonly LegalDocumentType = LegalDocumentType;
}
