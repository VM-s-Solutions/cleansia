import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-detail-skeleton',
  standalone: true,
  imports: [Skeleton],
  templateUrl: './cleansia-detail-skeleton.component.html',
  styles: `
    .cleansia-detail-skeleton {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      padding: 1.5rem;
    }
    .skeleton-breadcrumb {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .skeleton-header-card {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      padding: 1.5rem;
      background: white;
      border-radius: 8px;
      border: 1px solid #e5e7eb;
    }
    .skeleton-header-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .skeleton-badges {
      display: flex;
      gap: 0.5rem;
    }
    .skeleton-header-info {
      display: flex;
      gap: 2rem;
    }
    .skeleton-info-section {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      padding: 1.25rem;
      background: white;
      border-radius: 8px;
      border: 1px solid #e5e7eb;
    }
    .skeleton-info-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1.25rem;
    }
    .skeleton-info-item {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    @media (max-width: 640px) {
      .skeleton-info-grid { grid-template-columns: 1fr; }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaDetailSkeletonComponent {}
