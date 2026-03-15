import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
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

  showNewsletter = input(false);
  currentYear = new Date().getFullYear();

  submitRequest(form: any): void {
    this.snackbarService.showSuccessTranslated('global.messages.form.request_sent');
    form.reset();
  }
}
