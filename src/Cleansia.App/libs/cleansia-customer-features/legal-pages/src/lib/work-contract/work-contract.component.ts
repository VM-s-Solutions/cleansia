import { ChangeDetectionStrategy, Component } from '@angular/core';
import { LegalDocumentType } from '@cleansia/customer-services';
import { LegalDocumentComponent } from '../legal-document/legal-document.component';

@Component({
  selector: 'cleansia-customer-work-contract',
  standalone: true,
  imports: [LegalDocumentComponent],
  templateUrl: './work-contract.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkContractComponent {
  protected readonly LegalDocumentType = LegalDocumentType;
}
