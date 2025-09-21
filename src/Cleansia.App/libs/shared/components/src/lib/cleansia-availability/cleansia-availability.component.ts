import { CommonModule } from '@angular/common';
import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccordionModule } from 'primeng/accordion';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { ChipModule } from 'primeng/chip';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';

export interface TimeRange {
  start: string; // ISO time string, e.g., '10:00'
  end: string; // ISO time string, e.g., '12:00'
}

export interface Availability {
  [day: string]: TimeRange[]; // e.g., { 'MO': [{start: '10:00', end: '12:00'}] }
}

const DAYS = [
  { key: 'MO', label: 'Monday' },
  { key: 'TU', label: 'Tuesday' },
  { key: 'WE', label: 'Wednesday' },
  { key: 'TH', label: 'Thursday' },
  { key: 'FR', label: 'Friday' },
  { key: 'SA', label: 'Saturday' },
  { key: 'SU', label: 'Sunday' },
];

@Component({
  selector: 'cleansia-availability',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AccordionModule,
    ButtonModule,
    CalendarModule,
    InputTextModule,
    ChipModule,
    DialogModule,
    DividerModule,
  ],
  templateUrl: './cleansia-availability.component.html',
  styleUrls: ['./cleansia-availability.component.scss'],
  styles: [
    `
      .availability-module {
        max-width: 600px;
      }
      .day-content {
        padding: 1rem;
      }
      .field label {
        display: block;
        margin-bottom: 0.5rem;
        font-weight: 500;
      }
      .dialog-content {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }
      .ranges-list {
        display: flex;
        flex-wrap: wrap;
      }
    `,
  ],
})
export class CleansiaAvailabilityComponent {
  availability = input<Availability>({});
  days = DAYS;

  private _availability = signal<Availability>({});
  showDialog = false;
  currentDay: string | null = null;
  newRange = { start: '', end: '' } as TimeRange;

  availabilityChange = output<Availability>();

  ngOnInit() {
    this._availability.set(this.availability());
  }

  openAddDialog(day: string) {
    this.currentDay = day;
    this.newRange = { start: '', end: '' };
    this.showDialog = true;
  }

  resetDialog() {
    this.newRange = { start: '', end: '' };
    this.currentDay = null;
  }

  isValidRange(): boolean {
    if (!this.newRange.start || !this.newRange.end) return false;
    const startTime = new Date(
      `2000-01-01T${this.newRange.start}:00`
    ).getTime();
    const endTime = new Date(`2000-01-01T${this.newRange.end}:00`).getTime();
    return startTime < endTime;
  }

  addRange() {
    if (!this.isValidRange() || !this.currentDay) return;

    const dayRanges = this._availability()[this.currentDay] || [];
    dayRanges.push({ ...this.newRange });
    dayRanges.sort((a, b) => {
      const startA = new Date(`2000-01-01T${a.start}:00`).getTime();
      const startB = new Date(`2000-01-01T${b.start}:00`).getTime();
      return startA - startB;
    });

    this._availability.update((prev) => ({
      ...prev,
      [this.currentDay!]: dayRanges,
    }));

    this.showDialog = false;
    this.resetDialog();
  }

  removeRange(day: string, index: number) {
    const dayRanges = this._availability()[day] || [];
    dayRanges.splice(index, 1);
    this._availability.update((prev) => {
      const updated = { ...prev };
      if (dayRanges.length > 0) {
        updated[day] = dayRanges;
      } else {
        delete updated[day];
      }
      return updated;
    });
  }

  save() {
    // Clean up empty days
    const cleaned = Object.fromEntries(
      Object.entries(this._availability()).filter(
        ([_, ranges]) => ranges && ranges.length > 0
      )
    );
    this.availabilityChange.emit(cleaned);
  }
}
