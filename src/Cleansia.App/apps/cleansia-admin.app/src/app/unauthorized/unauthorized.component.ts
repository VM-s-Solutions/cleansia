import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
import { DialogService } from '@cleansia/services';
import {
  CleansiaButtonComponent,
  CleansiaDynamicBackgroundComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaDynamicBackgroundComponent,
    TranslatePipe,
  ],
  templateUrl: './unauthorized.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnauthorizedComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AdminAuthService);
  private readonly dialogService = inject(DialogService);

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  /**
   * Confirms first, matching the sidebar's own logout. This page is reached when a signed-in
   * admin lacks the role for a route, so the button sits beside "Go to login" — one recovers,
   * the other ends the session, and they were one misclick apart with no confirmation.
   */
  logout(): void {
    this.dialogService
      .confirmTranslated('global.dialog.confirm_logout', 'global.dialog.confirm')
      .subscribe((confirmed) => {
        if (confirmed) this.authService.logout().subscribe();
      });
  }
}
