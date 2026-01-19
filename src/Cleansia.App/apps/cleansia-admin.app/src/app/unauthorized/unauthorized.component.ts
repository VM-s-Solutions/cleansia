import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
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
  template: `
    <div class="cleansia-unauthorized">
      <cleansia-dynamic-background />
      <i class="fa-solid fa-lock cleansia-unauthorized__icons__lock"></i>
      <i class="fa-solid fa-shield cleansia-unauthorized__icons__shield"></i>
      <i class="fa-solid fa-user-slash cleansia-unauthorized__icons__user"></i>
      <i class="fa-solid fa-ban cleansia-unauthorized__icons__ban"></i>
      <div class="cleansia-unauthorized__container">
        <h1 class="cleansia-unauthorized__code">403</h1>
        <h2 class="cleansia-unauthorized__title">
          {{ 'pages.unauthorized.title' | translate }}
        </h2>
        <p class="cleansia-unauthorized__msg">
          {{ 'pages.unauthorized.message' | translate }}
        </p>
        <div class="cleansia-unauthorized__actions">
          <cleansia-button
            [label]="'global.actions.go_to_login' | translate"
            severity="primary"
            icon="pi pi-sign-in"
            (onClick)="goToLogin()"
          />
          <cleansia-button
            [label]="'global.actions.logout' | translate"
            severity="secondary"
            icon="pi pi-sign-out"
            [outlined]="true"
            (onClick)="logout()"
          />
        </div>
      </div>
    </div>
  `,
})
export class UnauthorizedComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AdminAuthService);

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
