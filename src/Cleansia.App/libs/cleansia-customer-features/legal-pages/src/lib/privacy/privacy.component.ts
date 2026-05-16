import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CleansiaTitleComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-customer-privacy',
  standalone: true,
  imports: [TranslatePipe, CleansiaTitleComponent],
  templateUrl: './privacy.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyComponent {
  sections = [1, 2, 3, 4, 5, 6];
}
