import { Component } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-form-skeleton',
  standalone: true,
  imports: [Skeleton],
  template: `
    <div class="cleansia-profile">
      <div class="cleansia-form-skeleton page-wrapper">
        <!-- Title -->
        <div class="skeleton-header">
          <p-skeleton width="180px" height="2rem" />
          <p-skeleton width="320px" height="1rem" styleClass="mt-2" />
        </div>

        <!-- Section 1: Personal Info -->
        <div class="skeleton-section">
          <p-skeleton width="160px" height="1.25rem" />
          <div class="skeleton-fields-grid">
            @for (i of [1, 2, 3]; track i) {
            <p-skeleton width="100%" height="3rem" borderRadius="6px" />
            }
            <div class="skeleton-field-full">
              <p-skeleton width="100%" height="3rem" borderRadius="6px" />
            </div>
            @for (i of [1, 2, 3, 4, 5, 6]; track i) {
            <p-skeleton width="100%" height="3rem" borderRadius="6px" />
            }
          </div>
        </div>

        <!-- Section 2: Bank Details -->
        <div class="skeleton-section">
          <p-skeleton width="140px" height="1.25rem" />
          <div class="skeleton-field-full">
            <p-skeleton width="100%" height="3rem" borderRadius="6px" />
          </div>
        </div>

        <!-- Section 3: Emergency Contact -->
        <div class="skeleton-section">
          <p-skeleton width="180px" height="1.25rem" />
          <div class="skeleton-fields-grid">
            @for (i of [1, 2]; track i) {
            <p-skeleton width="100%" height="3rem" borderRadius="6px" />
            }
          </div>
        </div>

        <!-- Submit Button -->
        <p-skeleton width="120px" height="2.75rem" borderRadius="6px" />
      </div>
    </div>
  `,
  styles: `
    .cleansia-form-skeleton {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
      padding: 2rem;
      max-width: 1200px;
      margin: 0 auto;
      width: 100%;
    }
    .skeleton-header {
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .skeleton-section {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      padding: 1.25rem;
      background: white;
      border-radius: 8px;
      border: 1px solid #e5e7eb;
    }
    .skeleton-fields-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1.25rem;
    }
    .skeleton-field-full {
      grid-column: 1 / -1;
    }
    @media (max-width: 968px) {
      .skeleton-fields-grid { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 640px) {
      .skeleton-fields-grid { grid-template-columns: 1fr; }
      .cleansia-form-skeleton { padding: 0.75rem; }
    }
  `,
})
export class CleansiaFormSkeletonComponent {}
