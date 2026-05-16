import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-dashboard-skeleton',
  standalone: true,
  imports: [Skeleton],
  templateUrl: './cleansia-dashboard-skeleton.component.html',
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaDashboardSkeletonComponent {}
