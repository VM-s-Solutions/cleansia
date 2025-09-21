import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaSectionComponent,
  CleansiaTableAction,
  CleansiaTableColumn,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTimePickerComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DialogService } from 'primeng/dynamicdialog';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { OrdersFacade } from './orders.facade';

@Component({
  selector: 'cleansia-partner-orders',
  standalone: true,
  imports: [
    TableModule,
    ToastModule,
    DialogModule,
    CommonModule,
    ButtonModule,
    TranslatePipe,
    CalendarModule,
    ReactiveFormsModule,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    CleansiaCalendarComponent,
    CleansiaTextInputComponent,
    CleansiaTimePickerComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss'],
  providers: [OrdersFacade, DialogService],
})
export class OrdersComponent {
  protected readonly facade = inject(OrdersFacade);

  // Orders table configuration
  ordersColumns: CleansiaTableColumn[] = [
    { field: 'id', header: 'pages.orders.order_id', sortable: true },
    { field: 'title', header: 'pages.orders.title', sortable: true },
    { field: 'description', header: 'pages.orders.description' },
    {
      field: 'dueDate',
      header: 'pages.orders.due_date',
      pipe: 'date',
      sortable: true,
    },
    { field: 'status', header: 'pages.orders.status' },
  ];

  ordersActions: CleansiaTableAction[] = [
    {
      label: 'pages.orders.log_time',
      icon: 'pi pi-clock',
      class: 'p-button-outlined p-button-sm',
      action: (order) => this.facade.openTimeLogDialog(order),
    },
  ];

  // Time logs table configuration
  timeLogsColumns: CleansiaTableColumn[] = [
    { field: 'orderId', header: 'pages.orders.order_id', sortable: true },
    {
      field: 'date',
      header: 'pages.orders.date',
      pipe: 'date',
      sortable: true,
    },
    { field: 'startTime', header: 'pages.orders.start_time' },
    { field: 'endTime', header: 'pages.orders.end_time' },
    { field: 'totalHours', header: 'pages.orders.total_hours', pipe: 'number' },
    { field: 'notes', header: 'pages.orders.notes' },
  ];

  timeLogsActions: CleansiaTableAction[] = [
    {
      label: 'pages.orders.edit',
      icon: 'pi pi-pencil',
      class: 'p-button-outlined p-button-sm',
      action: (log) => this.facade.editTimeLog(log),
    },
    {
      label: 'pages.orders.delete',
      icon: 'pi pi-trash',
      class: 'p-button-outlined p-button-danger p-button-sm',
      action: (log) => this.facade.deleteTimeLog(log),
    },
  ];

  calculateHours(log: any): number {
    return this.facade.calculateHours(log);
  }

  getFieldValue(item: any, column: CleansiaTableColumn): any {
    const value = this.getNestedValue(item, column.field);
    return this.applyPipe(value, column.pipe, column.pipeArgs);
  }

  private getNestedValue(obj: any, path: string): any {
    return path.split('.').reduce((current, prop) => current?.[prop], obj);
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
      default:
        return value;
    }
  }
}
