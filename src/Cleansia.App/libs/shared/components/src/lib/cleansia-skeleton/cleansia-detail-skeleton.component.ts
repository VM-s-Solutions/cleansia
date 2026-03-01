import { Component } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-detail-skeleton',
  standalone: true,
  imports: [Skeleton],
  template: `
    <div class="cleansia-detail-skeleton page-wrapper">
      <!-- Back button + Title row -->
      <div class="skeleton-breadcrumb">
        <p-skeleton shape="circle" size="2rem" />
        <p-skeleton width="80px" height="1rem" />
        <p-skeleton width="6px" height="1rem" />
        <p-skeleton width="120px" height="1rem" />
      </div>

      <!-- Header card -->
      <div class="skeleton-header-card">
        <div class="skeleton-header-top">
          <p-skeleton width="200px" height="1.75rem" />
          <div class="skeleton-badges">
            <p-skeleton width="80px" height="1.5rem" borderRadius="12px" />
            <p-skeleton width="80px" height="1.5rem" borderRadius="12px" />
          </div>
        </div>
        <div class="skeleton-header-info">
          <p-skeleton width="160px" height="1rem" />
          <p-skeleton width="120px" height="1rem" />
        </div>
      </div>

      <!-- Info Sections -->
      @for (section of [1, 2, 3]; track section) {
        <div class="skeleton-info-section">
          <p-skeleton width="160px" height="1.25rem" />
          <div class="skeleton-info-grid">
            @for (i of [1, 2, 3, 4]; track i) {
              <div class="skeleton-info-item">
                <p-skeleton width="80px" height="0.75rem" />
                <p-skeleton width="100%" height="3rem" borderRadius="6px" />
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
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
})
export class CleansiaDetailSkeletonComponent {}
