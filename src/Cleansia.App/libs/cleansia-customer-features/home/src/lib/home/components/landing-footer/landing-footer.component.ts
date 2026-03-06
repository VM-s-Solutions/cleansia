import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CleansiaBrandNameComponent } from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'cleansia-landing-footer',
  templateUrl: './landing-footer.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterModule,
    TranslateModule,
    ButtonModule,
    CleansiaBrandNameComponent,
  ],
})
export class LandingFooterComponent {
  private readonly snackbarService = inject(SnackbarService);

  currentYear = new Date().getFullYear();

  submitRequest(form: any): void {
    this.snackbarService.showSuccessTranslated('global.messages.form.request_sent');
    form.reset();
  }
}
