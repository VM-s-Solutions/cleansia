import { Injectable, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';

export interface Order {
  id: number;
  title: string;
  description: string;
  dueDate: Date;
  status: 'Pending' | 'In Progress' | 'Completed';
}

export interface TimeLog {
  id?: number;
  orderId: number;
  date: Date;
  startTime: string;
  endTime: string;
  notes: string;
}

@Injectable()
export class OrdersFacade {
  private readonly formBuilder = inject(FormBuilder);
  private readonly messageService = inject(MessageService);
  private readonly dialogService = inject(DialogService);

  // Signals for reactive data
  activeOrders = signal<Order[]>([
    // Sample data; replace with API fetch
    {
      id: 1,
      title: 'Website Redesign',
      description: 'Update UI for client site',
      dueDate: new Date('2025-10-01'),
      status: 'In Progress',
    },
    {
      id: 2,
      title: 'Bug Fixes',
      description: 'Resolve reported issues',
      dueDate: new Date('2025-09-25'),
      status: 'Pending',
    },
  ]);
  timeLogs = signal<TimeLog[]>([
    // Sample data; replace with API fetch
    {
      id: 1,
      orderId: 1,
      date: new Date('2025-09-17'),
      startTime: '09:00',
      endTime: '12:00',
      notes: 'UI updates',
    },
  ]);
  showDialog = signal(false);
  selectedOrder = signal<Order | null>(null);
  isSubmitting = signal(false);

  timeLogForm: FormGroup = this.formBuilder.group({
    date: [new Date(), [Validators.required]],
    startTime: ['', [Validators.required]],
    endTime: ['', [Validators.required]],
    notes: ['', [Validators.maxLength(500)]],
  });

  // Computed for total hours per log
  calculateHours(log: TimeLog): number {
    const start = new Date(`2000-01-01T${log.startTime}:00`).getTime();
    const end = new Date(`2000-01-01T${log.endTime}:00`).getTime();
    return (end - start) / (1000 * 60 * 60); // Hours
  }

  openTimeLogDialog(order: Order): void {
    this.selectedOrder.set(order);
    this.timeLogForm.patchValue({
      date: new Date(),
      startTime: '',
      endTime: '',
      notes: '',
    });
    this.showDialog.set(true);
  }

  resetDialog(): void {
    this.timeLogForm.reset({
      date: new Date(),
      startTime: '',
      endTime: '',
      notes: '',
    });
    this.selectedOrder.set(null);
  }

  saveTimeLog(): void {
    if (this.timeLogForm.valid && this.selectedOrder()) {
      const logData: TimeLog = {
        orderId: this.selectedOrder()!.id,
        ...this.timeLogForm.value,
      };
      this.timeLogs.update((prev) => [...prev, { ...logData, id: Date.now() }]); // Mock ID; use real from API
      this.messageService.add({
        severity: 'success',
        summary: 'Success',
        detail: 'Time log saved successfully.',
      });
      this.showDialog.set(false);
      this.resetDialog();
    } else {
      this.messageService.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Please fill in all required fields.',
      });
    }
  }

  editTimeLog(log: TimeLog): void {
    // Open dialog with pre-filled data
    const order = this.activeOrders().find((o) => o.id === log.orderId);
    if (order) {
      this.openTimeLogDialog(order);
      this.timeLogForm.patchValue({
        date: log.date,
        startTime: log.startTime,
        endTime: log.endTime,
        notes: log.notes,
      });
    }
  }

  deleteTimeLog(log: TimeLog): void {
    this.timeLogs.update((prev) => prev.filter((l) => l.id !== log.id));
    this.messageService.add({
      severity: 'warn',
      summary: 'Deleted',
      detail: 'Time log removed.',
    });
  }

  onSubmitReport(): void {
    this.isSubmitting.set(true);
    // Simulate API call for weekly report submission
    setTimeout(() => {
      this.messageService.add({
        severity: 'info',
        summary: 'Submitted',
        detail: 'Your time report has been submitted for review.',
      });
      this.isSubmitting.set(false);
      console.log('Time Logs Report:', this.timeLogs());
    }, 2000);
  }
}
