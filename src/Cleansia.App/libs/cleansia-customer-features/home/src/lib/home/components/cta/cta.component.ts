import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { FoamEdgeComponent } from '../foam-edge/foam-edge.component';
import { PromoRequestFacade } from './promo-request.facade';

/**
 * The closing card: the last call to act, and the first-order promo capture.
 *
 * The capture moved here from the footer. It is the same ask made a second way
 * — leave now, or leave an address — and putting it under the navigation meant
 * nobody read it.
 */
@Component({
  selector: 'cleansia-cta',
  templateUrl: './cta.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterModule, TranslatePipe, FoamEdgeComponent],
  providers: [PromoRequestFacade],
})
export class CtaComponent {
  readonly promo = inject(PromoRequestFacade);
  readonly consented = signal(false);

  toggleConsent(): void {
    this.consented.update((v) => !v);
    this.promo.reset();
  }

  submitPromo(form: NgForm): void {
    const email = (form.value as { email?: string })?.email ?? '';
    this.promo.request(email, this.consented());
  }
}
