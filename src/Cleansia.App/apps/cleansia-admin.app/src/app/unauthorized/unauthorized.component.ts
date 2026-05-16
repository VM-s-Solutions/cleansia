import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
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

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  logout(): void {
    this.authService.logout().subscribe();
  }
}
