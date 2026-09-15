import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServiceDetails } from '@cleansia/partner-services';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-partner-order-additional-services',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './order-additional-services.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderAdditionalServicesComponent {
  services = input<ServiceDetails[]>();
}
