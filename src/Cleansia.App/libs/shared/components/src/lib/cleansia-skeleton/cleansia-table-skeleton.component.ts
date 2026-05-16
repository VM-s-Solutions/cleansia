import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-table-skeleton',
  standalone: true,
  imports: [Skeleton],
  templateUrl: './cleansia-table-skeleton.component.html',
  styles: `
    .cleansia-table-skeleton {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .skeleton-title-area {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .skeleton-filter-bar {
      display: flex;
      justify-content: flex-end;
      margin-top: 0.5rem;
    }
    .skeleton-table-header {
      display: grid;
      gap: 1rem;
      padding: 1rem;
      background: #f8fafc;
      border-radius: 8px 8px 0 0;
      border: 1px solid #e5e7eb;
    }
    .skeleton-table-row {
      display: grid;
      gap: 1rem;
      padding: 1rem;
      border: 1px solid #e5e7eb;
      border-top: none;
    }
    .skeleton-table-row:last-child {
      border-radius: 0 0 8px 8px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaTableSkeletonComponent {
  columns = input(5);

  columnArray = computed(() =>
    Array.from({ length: this.columns() }, (_, i) => i + 1)
  );

  gridColumns = computed(() =>
    Array.from({ length: this.columns() }, () => '1fr').join(' ')
  );
}
