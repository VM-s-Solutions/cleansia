import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { CleansiaTitleComponent } from '@cleansia/components/cleansia-title';

/**
 * The operating rules, stated on the page rather than buried in the terms.
 *
 * Every number here is pinned in `docs/product/business-rules.md` and is the
 * same figure the pricing and cancellation code uses. This section replaces the
 * testimonials block, which rendered three invented customers and a hardcoded
 * five-star row against a system that aggregates no ratings.
 * -> /product/business-rules
 */
@Component({
  selector: 'cleansia-rules',
  templateUrl: './rules.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CleansiaTitleComponent],
})
export class RulesComponent {}
