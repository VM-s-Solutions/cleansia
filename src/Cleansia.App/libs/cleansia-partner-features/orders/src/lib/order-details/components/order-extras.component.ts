import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CleansiaSectionComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-partner-order-extras',
  standalone: true,
  imports: [CommonModule, CleansiaSectionComponent, TranslatePipe],
  templateUrl: './order-extras.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderExtrasComponent {
  extrasEntries = input<[string, boolean][]>();
}
