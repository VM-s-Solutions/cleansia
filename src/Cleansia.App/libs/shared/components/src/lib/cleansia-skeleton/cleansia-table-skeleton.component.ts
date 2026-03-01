import { Component, computed, input } from '@angular/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-table-skeleton',
  standalone: true,
  imports: [Skeleton],
  template: `
    <div class="cleansia-table-skeleton">
      <!-- Title area -->
      <div class="skeleton-title-area">
        <p-skeleton width="200px" height="2rem" />
        <p-skeleton width="300px" height="1rem" />
      </div>

      <!-- Filter bar -->
      <div class="skeleton-filter-bar">
        <p-skeleton width="120px" height="2.5rem" borderRadius="6px" />
      </div>

      <!-- Table Header -->
      <div
        class="skeleton-table-header"
        [style.grid-template-columns]="gridColumns()"
      >
        @for (i of columnArray(); track i) {
        <p-skeleton width="100%" height="1rem" />
        }
      </div>

      <!-- Table Rows -->
      @for (row of [1, 2, 3, 4, 5, 6]; track row) {
      <div
        class="skeleton-table-row"
        [style.grid-template-columns]="gridColumns()"
      >
        @for (col of columnArray(); track col) {
        <p-skeleton width="100%" height="1rem" />
        }
      </div>
      }
    </div>
  `,
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
