import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-customer-terms',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './terms.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TermsComponent {
  sections = [1, 2, 3, 4, 5, 6];
}
