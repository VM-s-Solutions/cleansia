import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-extras',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './order-extras.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderExtrasComponent {
  extrasEntries = input<[string, boolean][]>();
}
