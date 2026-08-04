import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { MembershipFacade } from './membership.facade';

/**
 * Post-purchase celebration page — replaces the silent
 * `/membership?subscribed=1` flow that left users staring at the management
 * card with no affirmation. Shown once after the Stripe Checkout success
 * URL redirect.
 *
 * Two CTAs (mobile parity):
 *  - Primary: "Set up a recurring cleaning" → routes to the recurring
 *    create wizard. The headline Plus perk we want users to discover first.
 *  - Secondary: "Back to membership" → returns to the management page.
 */
@Component({
  selector: 'cleansia-customer-membership-welcome',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, TranslatePipe, CleansiaButtonComponent],
  providers: [MembershipFacade],
  templateUrl: './membership-welcome.component.html',
})
export class MembershipWelcomeComponent implements OnInit {
  private readonly facade = inject(MembershipFacade);
  private readonly router = inject(Router);
  protected readonly routes = CleansiaCustomerRoute;

  /**
   * The express perk is the one benefit on this list the plan may not carry, so it is the one
   * that has to come from the server rather than from the template.
   */
  protected readonly expressWaiverAdvertised = this.facade.expressWaiverAdvertised;

  ngOnInit(): void {
    this.facade.refresh();
  }

  goToSetupRecurring(): void {
    this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP, 'recurring', 'create']);
  }
}
