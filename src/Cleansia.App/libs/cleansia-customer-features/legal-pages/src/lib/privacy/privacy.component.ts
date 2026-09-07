import { ChangeDetectionStrategy, Component } from '@angular/core';
import { LegalDocumentComponent } from '../legal-document/legal-document.component';

@Component({
  selector: 'cleansia-customer-privacy',
  standalone: true,
  imports: [LegalDocumentComponent],
  templateUrl: './privacy.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyComponent {
  sections = [1, 2, 3, 4, 5, 6];
}
