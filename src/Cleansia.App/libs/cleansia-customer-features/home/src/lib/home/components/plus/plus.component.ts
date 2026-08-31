import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Cleansia Plus, stated with the figures the seed actually pins:
 * DiscountPercentage 5.00, FreeCancellationWindowHours 4,
 * ExpressUpgradesPerMonth 1, TrialPeriodDays 14, 199 CZK monthly.
 *
 * The price is copy rather than a live read of `GetMembershipPlans` — worth
 * revisiting if plans ever vary by tenant or country.
 */
@Component({
  selector: 'cleansia-plus',
  templateUrl: './plus.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, RouterModule],
})
export class PlusComponent {}
