import { Component } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-dashboard-skeleton',
  standalone: true,
  imports: [Skeleton],
  template: `
    <div class="cleansia-dashboard-skeleton">
      <!-- Stat Cards -->
      <div class="skeleton-stats">
        @for (i of [1, 2, 3, 4]; track i) {
          <div class="skeleton-stat-card">
            <p-skeleton shape="circle" size="3rem" />
            <div class="skeleton-stat-content">
              <p-skeleton width="80px" height="0.875rem" />
              <p-skeleton width="60px" height="1.5rem" />
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: `
    .cleansia-dashboard-skeleton {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .skeleton-stats {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1rem;
    }
    .skeleton-stat-card {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1.25rem;
      background: white;
      border-radius: 8px;
      border: 1px solid #e5e7eb;
    }
    .skeleton-stat-content {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    @media (max-width: 768px) {
      .skeleton-stats { grid-template-columns: repeat(2, 1fr); }
    }
  `,
})
export class CleansiaDashboardSkeletonComponent {}
