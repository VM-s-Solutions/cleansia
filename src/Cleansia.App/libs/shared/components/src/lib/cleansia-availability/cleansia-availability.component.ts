import { CommonModule } from '@angular/common';
import { Component, forwardRef, signal } from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Accordion,
  AccordionContent,
  AccordionHeader,
  AccordionPanel,
} from 'primeng/accordion';
import { Button } from 'primeng/button';
import { Chip } from 'primeng/chip';
import { DatePicker } from 'primeng/datepicker';
import { Dialog } from 'primeng/dialog';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

export interface TimeRange {
  start: string; // ISO time string, e.g., '10:00'
  end: string; // ISO time string, e.g., '12:00'
}

export interface Availability {
  [day: string]: TimeRange[]; // e.g., { 'Monday': [{start: '10:00', end: '12:00'}] }
}

const DAYS = [
  { key: 'Monday', label: 'components.availability.days.monday' },
  { key: 'Tuesday', label: 'components.availability.days.tuesday' },
  { key: 'Wednesday', label: 'components.availability.days.wednesday' },
  { key: 'Thursday', label: 'components.availability.days.thursday' },
  { key: 'Friday', label: 'components.availability.days.friday' },
  { key: 'Saturday', label: 'components.availability.days.saturday' },
  { key: 'Sunday', label: 'components.availability.days.sunday' },
];

@Component({
  selector: 'cleansia-availability',
  standalone: true,
  imports: [
    Chip,
    Button,
    Dialog,
    Accordion,
    DatePicker,
    FormsModule,
    CommonModule,
    AccordionPanel,
    AccordionHeader,
    AccordionContent,
    TranslatePipe,
  ],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaAvailabilityComponent),
      multi: true,
    },
  ],
  templateUrl: './cleansia-availability.component.html',
})
export class CleansiaAvailabilityComponent extends CleansiaBaseFormInputComponent {
  // Constants
  readonly days = DAYS;

  // Signals for state management
  private readonly _availability = signal<Availability>({});
  readonly showDialog = signal(false);
  readonly currentDay = signal<string | null>(null);
  readonly newRange = signal<TimeRange>({ start: '', end: '' });

  // ControlValueAccessor override
  override writeValue(value: Availability | null): void {
    this._availability.set(value || {});
  }

  // Public getter for availability data
  availability(): Availability {
    return this._availability();
  }

  // Dialog management
  openAddDialog(day: string): void {
    this.currentDay.set(day);
    this.newRange.set({ start: '', end: '' });
    this.showDialog.set(true);
  }

  resetDialog(): void {
    this.newRange.set({ start: '', end: '' });
    this.currentDay.set(null);
  }

  // Helper methods for template bindings
  updateStartTime(time: Date | string): void {
    const timeString = this.formatTime(time);
    this.newRange.update((r) => ({ ...r, start: timeString }));
  }

  updateEndTime(time: Date | string): void {
    const timeString = this.formatTime(time);
    this.newRange.update((r) => ({ ...r, end: timeString }));
  }

  // Convert Date object or string to HH:mm format
  private formatTime(time: Date | string | null): string {
    if (!time) return '';

    if (time instanceof Date) {
      const hours = time.getHours().toString().padStart(2, '0');
      const minutes = time.getMinutes().toString().padStart(2, '0');
      return `${hours}:${minutes}`;
    }

    return time;
  }

  // Validation
  isValidRange(): boolean {
    const range = this.newRange();
    if (!range.start || !range.end) return false;

    // Ensure we have string format (not Date objects)
    const startStr = typeof range.start === 'string' ? range.start : '';
    const endStr = typeof range.end === 'string' ? range.end : '';

    if (!startStr || !endStr) return false;

    // Parse time strings to compare (HH:mm format)
    const startParts = startStr.split(':');
    const endParts = endStr.split(':');

    if (startParts.length !== 2 || endParts.length !== 2) return false;

    const startHours = parseInt(startParts[0], 10);
    const startMinutes = parseInt(startParts[1], 10);
    const endHours = parseInt(endParts[0], 10);
    const endMinutes = parseInt(endParts[1], 10);

    // Validate time values
    if (
      isNaN(startHours) ||
      isNaN(startMinutes) ||
      isNaN(endHours) ||
      isNaN(endMinutes) ||
      startHours < 0 ||
      startHours > 23 ||
      endHours < 0 ||
      endHours > 23 ||
      startMinutes < 0 ||
      startMinutes > 59 ||
      endMinutes < 0 ||
      endMinutes > 59
    ) {
      return false;
    }

    // Convert to minutes for comparison
    const startTotalMinutes = startHours * 60 + startMinutes;
    const endTotalMinutes = endHours * 60 + endMinutes;

    return endTotalMinutes > startTotalMinutes;
  }

  // Range management
  addRange(): void {
    const day = this.currentDay();
    if (!this.isValidRange() || !day) return;

    this.formControl.markAsTouched();
    this.onTouch();

    const currentRange = this.newRange();
    const dayRanges = this._availability()[day] || [];
    dayRanges.push({ ...currentRange });

    // Sort ranges by start time
    dayRanges.sort((a, b) => {
      const startA = new Date(`2000-01-01T${a.start}:00`).getTime();
      const startB = new Date(`2000-01-01T${b.start}:00`).getTime();
      return startA - startB;
    });

    const updatedAvailability = {
      ...this._availability(),
      [day]: dayRanges,
    };

    this._availability.set(updatedAvailability);
    this.onChange(updatedAvailability);

    this.showDialog.set(false);
    this.resetDialog();
  }

  removeRange(day: string, index: number): void {
    this.formControl.markAsTouched();
    this.onTouch();

    const dayRanges = [...(this._availability()[day] || [])];
    dayRanges.splice(index, 1);

    const updated = { ...this._availability() };
    if (dayRanges.length > 0) {
      updated[day] = dayRanges;
    } else {
      delete updated[day];
    }

    this._availability.set(updated);
    this.onChange(updated);
  }
}
