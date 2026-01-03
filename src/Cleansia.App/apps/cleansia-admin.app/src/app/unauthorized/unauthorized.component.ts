import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
import { CleansiaButtonComponent } from '@cleansia/components';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule, CleansiaButtonComponent],
  template: `
    <div class="unauthorized-container">
      <div class="unauthorized-content">
        <i class="pi pi-exclamation-triangle unauthorized-icon"></i>
        <h1>Access Denied</h1>
        <p>You don't have permission to access this area.</p>
        <p class="unauthorized-subtitle">
          Administrator privileges are required.
        </p>
        <div class="unauthorized-actions">
          <cleansia-button
            label="Go to Login"
            icon="pi pi-sign-in"
            (onClick)="goToLogin()"
            severity="primary"
          />
          <cleansia-button
            label="Logout"
            icon="pi pi-sign-out"
            (onClick)="logout()"
            severity="secondary"
            [outlined]="true"
          />
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .unauthorized-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        padding: 2rem;
      }

      .unauthorized-content {
        background: white;
        border-radius: 16px;
        padding: 3rem;
        text-align: center;
        max-width: 500px;
        box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
      }

      .unauthorized-icon {
        font-size: 4rem;
        color: #f59e0b;
        margin-bottom: 1.5rem;
      }

      h1 {
        font-size: 2rem;
        color: #1f2937;
        margin-bottom: 1rem;
        font-weight: 700;
      }

      p {
        color: #6b7280;
        font-size: 1.125rem;
        margin-bottom: 0.5rem;
      }

      .unauthorized-subtitle {
        font-size: 0.875rem;
        margin-bottom: 2rem;
      }

      .unauthorized-actions {
        display: flex;
        gap: 1rem;
        justify-content: center;
      }
    `,
  ],
})
export class UnauthorizedComponent {
  constructor(private router: Router, private authService: AdminAuthService) {}

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
