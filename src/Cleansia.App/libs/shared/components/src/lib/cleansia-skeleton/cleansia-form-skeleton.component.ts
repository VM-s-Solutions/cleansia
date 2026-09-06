import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-form-skeleton',
  standalone: true,
  imports: [Skeleton],
  templateUrl: './cleansia-form-skeleton.component.html',
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
      /* Was PrimeFlex's mt-2 on the second bar. Only the customer app loads PrimeFlex, so in
         partner and admin the two bars sat flush against each other. 0.5rem is what mt-2 was. */
      gap: 0.5rem;
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaFormSkeletonComponent {}
