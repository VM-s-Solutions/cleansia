import { ChangeDetectionStrategy, Component } from '@angular/core';
import { LegalDocumentComponent } from '../legal-document/legal-document.component';

@Component({
  selector: 'cleansia-customer-terms',
  standalone: true,
  imports: [LegalDocumentComponent],
  templateUrl: './terms.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TermsComponent {
  sections = [1, 2, 3, 4, 5, 6];
}
