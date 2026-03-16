import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { CleansiaDynamicBackgroundComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-customer-checkout-cancel',
  standalone: true,
  imports: [TranslatePipe, RouterLink, CleansiaDynamicBackgroundComponent],
  templateUrl: './checkout-cancel.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutCancelComponent {
  routes = CleansiaCustomerRoute;
}
