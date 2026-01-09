import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  contentChild,
  input,
  output,
  TemplateRef,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { TableModule } from 'primeng/table';
import {
  CleansiaTableAction,
  CleansiaTableColumn,
  TableColumn,
  TableColumnAction,
  TableDefinition,
} from './cleansia-table.models';

@Component({
  selector: 'cleansia-table',
  standalone: true,
  templateUrl: './cleansia-table.component.html',
  imports: [CommonModule, TableModule, TranslatePipe],
})
export class CleansiaTableComponent {
  data = input<any[]>([]);
  columns = input<CleansiaTableColumn[]>([]);
  actions = input<CleansiaTableAction[]>([]);
  tableDefinition = input<TableDefinition>();
  paginator = input(true);
  rows = input(10);
  responsiveLayout = input<'stack' | 'scroll'>('scroll');
  emptyMessage = input('global.no_data');
  loadingMessage = input('global.loading');
  loading = input(false);
  sortField = input<string>();
  sortOrder = input<number>();
  showHeader = input(true);
  tableClass = input('');
  rowsPerPageOptions = input([5, 10, 20, 50]);
  globalFilterFields = input<string[]>([]);
  actionsHeader = input('global.actions.actions');

  customBodyTemplate = contentChild<TemplateRef<any>>('bodyTemplate');
  customActionTemplate = contentChild<TemplateRef<any>>('actionTemplate');

  rowSelect = output<any>();
  rowUnselect = output<any>();
  sortChange = output<{ field: string; order: number }>();

  tableClasses = computed(() => `cleansia-table ${this.tableClass()}`);

  totalColumns = computed(() => {
    if (this.tableDefinition()) {
      return this.tableDefinition()!.columns.length;
    }
    const columnsCount = this.columns().length;
    const actionsCount =
      this.actions().length > 0 || this.customActionTemplate() ? 1 : 0;
    return columnsCount + actionsCount;
  });

  visibleActions = computed(() => {
    return (item: any) =>
      this.actions().filter((action) =>
        action.visible ? action.visible(item) : true
      );
  });

  getFieldValue(item: any, column: CleansiaTableColumn): any {
    const value = this.getNestedValue(item, column.field);
    return this.applyPipe(value, column.pipe, column.pipeArgs);
  }

  getColumnClasses(column: CleansiaTableColumn): string {
    let classes = column.headerClass || '';
    if (column.width) {
      classes += ` column-width-${column.width.replace(/\D/g, '')}`;
    }
    return classes.trim();
  }

  private getNestedValue(obj: any, path: string): any {
    return path.split('.').reduce((current, prop) => current?.[prop], obj);
  }

  getTableDefinitionValue(item: any, column: TableColumn): any {
    if (column.template) {
      return null; // Template will handle rendering
    }

    if (typeof column.value === 'string') {
      return this.getNestedValue(item, column.value);
    }

    if (typeof column.value === 'function') {
      return column.value(item);
    }

    return '';
  }

  onRowClick(item: any): void {
    if (this.tableDefinition()?.onRowClick) {
      this.tableDefinition()!.onRowClick!(item);
    }
  }

  onColumnAction(action: TableColumnAction, item: any): void {
    action.onClick(item);
  }
  onSort(event: any): void {
    this.sortChange.emit({
      field: event.field,
      order: event.order,
    });
  }

  private applyPipe(value: any, pipe?: string, pipeArgs?: any[]): any {
    if (!pipe || value == null) return value;

    switch (pipe) {
      case 'date':
        return new Date(value).toLocaleDateString('cs-CZ');
      case 'number':
        return Number(value).toLocaleString('cs-CZ', {
          minimumFractionDigits: 2,
          maximumFractionDigits: 2,
        });
      case 'currency':
        return new Intl.NumberFormat('cs-CZ', {
          style: 'currency',
          currency: 'CZK',
        }).format(value);
      default:
        return value;
    }
  }
}
