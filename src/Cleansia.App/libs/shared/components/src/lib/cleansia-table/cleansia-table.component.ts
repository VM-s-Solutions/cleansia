import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { ICleansiaSelectOption } from '../cleansia-select';
import {
  PaginationState,
  SortEvent,
  TableAction,
  TableColumn,
  TableConfig,
} from './cleansia-table.models';

/**
 * Cleansia Custom Table Component
 *
 * A modern, feature-rich table component with glass morphism design.
 *
 * Features:
 * - Pagination (enabled by default)
 * - Sorting
 * - Custom templates
 * - Row actions
 * - Loading states
 * - Responsive design
 *
 * @example
 * // With pagination (default behavior)
 * <cleansia-table
 *   [data]="items"
 *   [columns]="columns"
 *   [actions]="actions"
 * />
 *
 * @example
 * // Without pagination
 * <cleansia-table
 *   [data]="items"
 *   [columns]="columns"
 *   [config]="{ paginator: false }"
 * />
 *
 * @example
 * // Custom rows per page
 * <cleansia-table
 *   [data]="items"
 *   [columns]="columns"
 *   [config]="{ rows: 20, rowsPerPageOptions: [10, 20, 50, 100] }"
 * />
 *
 * @example
 * // Server-side pagination (lazy loading)
 * <cleansia-table
 *   [data]="items"
 *   [columns]="columns"
 *   [config]="{ lazy: true, totalRecords: totalCount }"
 *   [loading]="isLoading"
 *   (pageChange)="onPageChange($event)"
 *   (sortChange)="onSortChange($event)"
 * />
 */
@Component({
  selector: 'cleansia-table',
  standalone: true,
  templateUrl: './cleansia-table.component.html',
  imports: [CommonModule, TranslateModule, TooltipModule, FormsModule, SelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaTableComponent<T = any> {
  // Inputs
  data = input.required<T[]>();
  columns = input<TableColumn<T>[]>([]);
  actions = input<TableAction<T>[]>([]);
  config = input<TableConfig>({
    hover: true,
    paginator: true,
    rows: 10,
    rowsPerPageOptions: [1, 5, 10, 20, 50],
  });
  selectedRow = input<T | null>(null);
  loading = input(false);
  clickableRows = input(false);

  // Outputs
  rowClick = output<T>();
  rowSelect = output<T>();
  actionClick = output<{ action: TableAction<T>; row: T }>();
  pageChange = output<PaginationState>();
  sortChange = output<SortEvent>();

  // Internal state
  currentSort = signal<SortEvent | null>(null);
  paginationState = signal<PaginationState>({
    first: 0,
    rows: 10,
    page: 0,
    totalRecords: 0,
  });
  currentRowsPerPage = signal<number>(10);

  // Merged config with defaults
  mergedConfig = computed(() => ({
    hover: true,
    paginator: true,
    rows: 10,
    rowsPerPageOptions: [5, 10, 20, 50],
    lazy: false,
    ...this.config(),
  }));

  // Rows per page options for select component
  rowsPerPageSelectOptions = computed<ICleansiaSelectOption[]>(() => {
    const options = this.mergedConfig().rowsPerPageOptions || [5, 10, 20, 50];
    return options.map((option) => ({
      label: option.toString(),
      value: option,
    }));
  });

  // Computed
  sortedData = computed(() => {
    // Skip sorting if lazy loading (server handles it)
    if (this.mergedConfig().lazy) {
      return this.data();
    }

    let result = [...this.data()];

    // Apply sorting
    const sort = this.currentSort();
    if (sort) {
      result = this.sortData(result, sort);
    }

    return result;
  });

  totalRecords = computed(() => {
    // Use totalRecords from config if lazy loading
    if (this.mergedConfig().lazy) {
      return this.mergedConfig().totalRecords || 0;
    }
    return this.sortedData().length;
  });

  paginatedData = computed(() => {
    const result = this.sortedData();

    // Skip pagination if lazy loading (server handles it)
    if (this.mergedConfig().lazy) {
      return result;
    }

    // Apply pagination
    if (this.mergedConfig().paginator) {
      const state = this.paginationState();
      const start = state.first;
      const end = start + state.rows;
      return result.slice(start, end);
    }

    return result;
  });

  totalPages = computed(() => {
    const total = this.totalRecords();
    const rows = this.paginationState().rows;
    return Math.ceil(total / rows);
  });

  currentPageNumber = computed(() => this.paginationState().page + 1);

  visiblePageNumbers = computed(() => {
    const total = this.totalPages();
    const current = this.currentPageNumber();
    const pages: (number | string)[] = [];

    if (total <= 7) {
      for (let i = 1; i <= total; i++) {
        pages.push(i);
      }
    } else {
      pages.push(1);

      if (current > 3) {
        pages.push('...');
      }

      for (
        let i = Math.max(2, current - 1);
        i <= Math.min(current + 1, total - 1);
        i++
      ) {
        pages.push(i);
      }

      if (current < total - 2) {
        pages.push('...');
      }

      pages.push(total);
    }

    return pages;
  });

  ngOnInit() {
    // Initialize pagination with merged config defaults
    const rows = this.mergedConfig().rows;
    this.paginationState.set({
      first: 0,
      rows: rows,
      page: 0,
      totalRecords: this.data().length,
    });
    this.currentRowsPerPage.set(rows);
  }

  // Methods
  getCellValue(row: T, column: TableColumn<T>): any {
    if (column.getValue) {
      return column.getValue(row);
    }
    return this.getNestedValue(row, column.field);
  }

  private getNestedValue(obj: any, path: string): any {
    return path.split('.').reduce((current, prop) => current?.[prop], obj);
  }

  onRowClick(row: T): void {
    if (this.mergedConfig().selectable) {
      this.rowSelect.emit(row);
    }
    this.rowClick.emit(row);
  }

  onActionClick(action: TableAction<T>, row: T, event: Event): void {
    event.stopPropagation();
    if (!action.disabled?.(row)) {
      action.onClick(row);
      this.actionClick.emit({ action, row });
    }
  }

  isRowSelected(row: T): boolean {
    return this.selectedRow() === row;
  }

  isActionVisible(action: TableAction<T>, row: T): boolean {
    return action.visible ? action.visible(row) : true;
  }

  isActionDisabled(action: TableAction<T>, row: T): boolean {
    return action.disabled ? action.disabled(row) : false;
  }

  onSort(column: TableColumn<T>): void {
    if (!column.sortable) return;

    const currentSort = this.currentSort();
    let newOrder: 1 | -1 = 1;

    if (currentSort?.field === column.field) {
      newOrder = currentSort.order === 1 ? -1 : 1;
    }

    const sortEvent: SortEvent = { field: column.field, order: newOrder };
    this.currentSort.set(sortEvent);
    this.sortChange.emit(sortEvent);
  }

  getSortIcon(column: TableColumn<T>): string {
    if (!column.sortable) return '';

    const currentSort = this.currentSort();
    if (currentSort?.field !== column.field) {
      return 'pi pi-sort-alt';
    }

    return currentSort.order === 1
      ? 'pi pi-sort-amount-up'
      : 'pi pi-sort-amount-down';
  }

  private sortData(data: T[], sort: SortEvent): T[] {
    return [...data].sort((a, b) => {
      const aVal = this.getNestedValue(a, sort.field);
      const bVal = this.getNestedValue(b, sort.field);

      if (aVal === bVal) return 0;
      if (aVal == null) return 1;
      if (bVal == null) return -1;

      const comparison = aVal < bVal ? -1 : 1;
      return sort.order * comparison;
    });
  }

  onPageChange(page: number): void {
    if (
      page < 1 ||
      page > this.totalPages() ||
      page === this.currentPageNumber()
    ) {
      return;
    }

    const newState: PaginationState = {
      ...this.paginationState(),
      first: (page - 1) * this.paginationState().rows,
      page: page - 1,
    };

    this.paginationState.set(newState);
    this.pageChange.emit(newState);
  }

  onPreviousPage(): void {
    const current = this.currentPageNumber();
    if (current > 1) {
      this.onPageChange(current - 1);
    }
  }

  onNextPage(): void {
    const current = this.currentPageNumber();
    if (current < this.totalPages()) {
      this.onPageChange(current + 1);
    }
  }

  onRowsPerPageChange(newRows: number): void {
    const newState: PaginationState = {
      ...this.paginationState(),
      rows: newRows,
      first: 0,
      page: 0,
    };

    this.currentRowsPerPage.set(newRows);
    this.paginationState.set(newState);
    this.pageChange.emit(newState);
  }

  getActionColor(color?: string): string {
    const colorMap: Record<string, string> = {
      warning: '#f59e0b',
      danger: '#ef4444',
      success: '#10b981',
      info: '#3b82f6',
      primary: '#0ea5e9',
    };
    return colorMap[color || ''] || '#6b7280';
  }

  trackByFn(index: number, item: T): any {
    return (item as any).id || index;
  }
}
