import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { CleansiaBrandNameComponent } from '../cleansia-brand-name/cleansia-brand-name.component';

@Component({
  selector: 'cleansia-customer-footer',
  templateUrl: './cleansia-customer-footer.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterModule,
    TranslatePipe,
    ButtonModule,
    CleansiaBrandNameComponent,
  ],
})
export class CleansiaCustomerFooterComponent {
  private readonly snackbarService = inject(SnackbarService);
  private readonly authService = inject(CustomerAuthService);

  showNewsletter = input(false);
  currentYear = new Date().getFullYear();

  // Hide the guest lookup link for logged-in users — they have /orders.
  private readonly isLoggedInSignal = signal(this.authService.isLoggedIn());
  readonly isAnonymous = computed(() => !this.isLoggedInSignal());

  submitRequest(form: any): void {
    this.snackbarService.showSuccessTranslated('global.messages.form.request_sent');
    form.reset();
  }
}
