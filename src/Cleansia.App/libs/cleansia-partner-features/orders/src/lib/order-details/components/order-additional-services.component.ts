import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-additional-services',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './order-additional-services.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderAdditionalServicesComponent {
  services = input<any[]>();
}
