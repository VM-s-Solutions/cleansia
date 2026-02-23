import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  forwardRef,
  inject,
  signal,
} from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DatePicker } from 'primeng/datepicker';
import { Dialog } from 'primeng/dialog';
import { InputSwitch } from 'primeng/inputswitch';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';
import { CleansiaButtonComponent } from '../cleansia-button/cleansia-button.component';

export interface TimeRange {
  start: string; // ISO time string, e.g., '10:00'
  end: string; // ISO time string, e.g., '12:00'
}

export interface Availability {
  [day: string]: TimeRange[]; // e.g., { 'Monday': [{start: '10:00', end: '12:00'}] }
}

export interface CalendarDay {
  date: Date;
  dayNumber: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isSelected: boolean;
  isWorkingDay: boolean;
  isOverride: boolean;
  isOverrideOff: boolean;
  timeSlots: TimeRange[];
  dotClass: string;
}

const DAYS = [
  { key: 'Monday', label: 'components.availability.days.monday', shortKey: 'components.availability.days.monday_short' },
  { key: 'Tuesday', label: 'components.availability.days.tuesday', shortKey: 'components.availability.days.tuesday_short' },
  { key: 'Wednesday', label: 'components.availability.days.wednesday', shortKey: 'components.availability.days.wednesday_short' },
  { key: 'Thursday', label: 'components.availability.days.thursday', shortKey: 'components.availability.days.thursday_short' },
  { key: 'Friday', label: 'components.availability.days.friday', shortKey: 'components.availability.days.friday_short' },
  { key: 'Saturday', label: 'components.availability.days.saturday', shortKey: 'components.availability.days.saturday_short' },
  { key: 'Sunday', label: 'components.availability.days.sunday', shortKey: 'components.availability.days.sunday_short' },
];

const DAY_INDEX_MAP: Record<number, string> = {
  1: 'Monday',
  2: 'Tuesday',
  3: 'Wednesday',
  4: 'Thursday',
  5: 'Friday',
  6: 'Saturday',
  0: 'Sunday',
};

@Component({
  selector: 'cleansia-availability',
  standalone: true,
  imports: [
    CleansiaButtonComponent,
    Dialog,
    DatePicker,
    InputSwitch,
    FormsModule,
    CommonModule,
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
  private readonly translate = inject(TranslateService);
  readonly days = DAYS;

  get dayHeaders(): string[] {
    return DAYS.map((d) => this.translate.instant(d.shortKey));
  }

  // State signals
  private readonly _availability = signal<Availability>({});
  readonly currentMonth = signal(new Date());
  readonly selectedDate = signal<Date>(new Date());
  readonly showTimeDialog = signal(false);
  readonly showExceptionDialog = signal(false);
  readonly showPresetDialog = signal(false);

  // Time dialog state — use Date objects directly to avoid ngModel reference issues
  readonly editingDay = signal<string | null>(null);
  readonly editingSlots = signal<{ start: Date; end: Date }[]>([]);

  // Exception dialog state
  readonly exceptionCustomHours = signal(false);
  readonly exceptionSlots = signal<{ start: Date; end: Date }[]>([]);

  // Preset dialog state
  readonly presetDays = signal<Set<string>>(
    new Set(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'])
  );
  readonly presetStartDate = signal(this.timeToDate('09:00'));
  readonly presetEndDate = signal(this.timeToDate('17:00'));

  // ControlValueAccessor — convert plain date keys (yyyy-MM-dd) from the API
  // to internal override_ prefixed keys for calendar UI logic
  override writeValue(value: Availability | null): void {
    if (!value) {
      this._availability.set({});
      return;
    }
    const internal: Availability = {};
    const dayNames = new Set(DAYS.map((d) => d.key));
    for (const key of Object.keys(value)) {
      if (dayNames.has(key)) {
        internal[key] = value[key];
      } else {
        // Date key from API (e.g. "2025-02-15") → internal "override_2025-02-15"
        internal[`override_${key}`] = value[key];
      }
    }
    this._availability.set(internal);
  }

  availability(): Availability {
    return this._availability();
  }

  // Computed: set of active day keys for fast lookup (avoids method calls in template)
  readonly activeDayKeys = computed(() => {
    const avail = this._availability();
    return new Set(DAYS.filter((d) => (avail[d.key] || []).length > 0).map((d) => d.key));
  });

  // Computed: active days for schedule editor
  readonly activeDays = computed(() => {
    const avail = this._availability();
    return DAYS.filter((d) => (avail[d.key] || []).length > 0).map((d) => ({
      ...d,
      ranges: avail[d.key] || [],
    }));
  });

  // Computed: status badge class
  readonly statusBadgeClass = computed(() => {
    const info = this.selectedDayInfo();
    if (info.isOverrideOff) return 'badge-off';
    if (info.isOverride && info.isWorkingDay) return 'badge-custom';
    if (info.isWorkingDay) return 'badge-working';
    return 'badge-day-off';
  });

  // Computed: status label translation key
  readonly statusLabel = computed(() => {
    const info = this.selectedDayInfo();
    if (info.isOverrideOff) return 'components.availability.exception_day_off';
    if (info.isOverride && info.isWorkingDay)
      return 'components.availability.custom_hours';
    if (info.isWorkingDay) return 'components.availability.working_day';
    return 'components.availability.day_off';
  });

  // Calendar computed values
  readonly monthLabel = computed(() => {
    const d = this.currentMonth();
    return d.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
  });

  readonly calendarRows = computed(() => {
    const month = this.currentMonth();
    const avail = this._availability();
    const selected = this.selectedDate();
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const year = month.getFullYear();
    const monthIdx = month.getMonth();
    const firstDay = new Date(year, monthIdx, 1);
    let startOffset = firstDay.getDay() - 1;
    if (startOffset < 0) startOffset = 6;

    const daysInMonth = new Date(year, monthIdx + 1, 0).getDate();
    const totalCells = startOffset + daysInMonth;
    const rowCount = Math.ceil(totalCells / 7);

    const rows: CalendarDay[][] = [];

    for (let row = 0; row < rowCount; row++) {
      const week: CalendarDay[] = [];
      for (let col = 0; col < 7; col++) {
        const cellIndex = row * 7 + col;
        const dayNumber = cellIndex - startOffset + 1;

        if (dayNumber >= 1 && dayNumber <= daysInMonth) {
          const date = new Date(year, monthIdx, dayNumber);
          const dayOfWeek = DAY_INDEX_MAP[date.getDay()];
          const dayRanges = avail[dayOfWeek] || [];
          const isWorkingDay = dayRanges.length > 0;

          const dateKey = this.formatDateKey(date);
          const overrideKey = `override_${dateKey}`;
          const hasOverride = avail[overrideKey] !== undefined;
          const overrideRanges = hasOverride ? avail[overrideKey] : [];

          const dateNorm = new Date(date);
          dateNorm.setHours(0, 0, 0, 0);
          const selectedNorm = new Date(selected);
          selectedNorm.setHours(0, 0, 0, 0);

          const effectiveWorking = hasOverride
            ? overrideRanges.length > 0
            : isWorkingDay;
          const isOverrideOff = hasOverride && overrideRanges.length === 0;

          let dotClass = '';
          if (isOverrideOff) dotClass = 'dot-off';
          else if (hasOverride && effectiveWorking) dotClass = 'dot-custom';
          else if (effectiveWorking) dotClass = 'dot-working';

          week.push({
            date,
            dayNumber,
            isCurrentMonth: true,
            isToday: dateNorm.getTime() === today.getTime(),
            isSelected: dateNorm.getTime() === selectedNorm.getTime(),
            isWorkingDay: effectiveWorking,
            isOverride: hasOverride,
            isOverrideOff,
            timeSlots: hasOverride ? overrideRanges : dayRanges,
            dotClass,
          });
        } else {
          week.push({
            date: new Date(0),
            dayNumber: 0,
            isCurrentMonth: false,
            isToday: false,
            isSelected: false,
            isWorkingDay: false,
            isOverride: false,
            isOverrideOff: false,
            timeSlots: [],
            dotClass: '',
          });
        }
      }
      rows.push(week);
    }
    return rows;
  });

  readonly selectedDayInfo = computed(() => {
    const selected = this.selectedDate();
    const avail = this._availability();
    const selectedNorm = new Date(selected);
    selectedNorm.setHours(0, 0, 0, 0);

    const dayOfWeek = DAY_INDEX_MAP[selectedNorm.getDay()];
    const dayRanges = avail[dayOfWeek] || [];
    const dateKey = this.formatDateKey(selectedNorm);
    const overrideKey = `override_${dateKey}`;
    const hasOverride = avail[overrideKey] !== undefined;
    const overrideRanges = hasOverride ? avail[overrideKey] : [];

    const isWorkingDay = hasOverride
      ? overrideRanges.length > 0
      : dayRanges.length > 0;
    const timeSlots = hasOverride ? overrideRanges : dayRanges;

    const dayName = selectedNorm.toLocaleDateString(undefined, {
      weekday: 'long',
    });
    const dateFormatted = selectedNorm.toLocaleDateString(undefined, {
      month: 'long',
      day: 'numeric',
      year: 'numeric',
    });

    return {
      dayName,
      dateFormatted,
      isWorkingDay,
      isOverride: hasOverride,
      isOverrideOff: hasOverride && overrideRanges.length === 0,
      timeSlots,
      dayOfWeek,
    };
  });

  // Calendar navigation
  previousMonth(): void {
    const d = new Date(this.currentMonth());
    d.setMonth(d.getMonth() - 1);
    this.currentMonth.set(d);
  }

  nextMonth(): void {
    const d = new Date(this.currentMonth());
    d.setMonth(d.getMonth() + 1);
    this.currentMonth.set(d);
  }

  selectDate(day: CalendarDay): void {
    if (!day.isCurrentMonth) return;
    this.selectedDate.set(day.date);
  }

  toggleDay(dayKey: string): void {
    this.formControl.markAsTouched();
    this.onTouch();

    const updated = { ...this._availability() };
    if ((updated[dayKey] || []).length > 0) {
      delete updated[dayKey];
    } else {
      updated[dayKey] = [{ start: '09:00', end: '17:00' }];
    }
    this._availability.set(updated);
    this.emitValue(updated);
  }

  // Edit time for a specific day
  openEditTimeDialog(dayKey: string): void {
    const avail = this._availability();
    const ranges = avail[dayKey] || [{ start: '09:00', end: '17:00' }];
    this.editingDay.set(dayKey);
    this.editingSlots.set(ranges.map((r) => ({ start: this.timeToDate(r.start), end: this.timeToDate(r.end) })));
    this.showTimeDialog.set(true);
  }

  addEditingSlot(): void {
    this.editingSlots.update((s) => [...s, { start: this.timeToDate('09:00'), end: this.timeToDate('17:00') }]);
  }

  removeEditingSlot(index: number): void {
    this.editingSlots.update((s) => s.filter((_, i) => i !== index));
  }

  updateEditingSlotTime(index: number, isStart: boolean, time: Date): void {
    this.editingSlots.update((s) =>
      s.map((slot, i) =>
        i === index
          ? isStart
            ? { ...slot, start: time }
            : { ...slot, end: time }
          : slot
      )
    );
  }

  saveEditingSlots(): void {
    const dayKey = this.editingDay();
    if (!dayKey) return;

    this.formControl.markAsTouched();
    this.onTouch();

    const updated = { ...this._availability() };
    const slots: TimeRange[] = this.editingSlots()
      .map((s) => ({ start: this.dateToTime(s.start), end: this.dateToTime(s.end) }))
      .filter((s) => s.start < s.end);
    if (slots.length > 0) {
      updated[dayKey] = slots.sort((a, b) => a.start.localeCompare(b.start));
    } else {
      delete updated[dayKey];
    }
    this._availability.set(updated);
    this.emitValue(updated);
    this.showTimeDialog.set(false);
  }

  // Exception management
  openExceptionDialog(): void {
    this.exceptionCustomHours.set(false);
    this.exceptionSlots.set([{ start: this.timeToDate('09:00'), end: this.timeToDate('17:00') }]);
    this.showExceptionDialog.set(true);
  }

  addExceptionSlot(): void {
    this.exceptionSlots.update((s) => [...s, { start: this.timeToDate('09:00'), end: this.timeToDate('17:00') }]);
  }

  removeExceptionSlot(index: number): void {
    this.exceptionSlots.update((s) => s.filter((_, i) => i !== index));
  }

  updateExceptionSlotTime(index: number, isStart: boolean, time: Date): void {
    this.exceptionSlots.update((s) =>
      s.map((slot, i) =>
        i === index
          ? isStart
            ? { ...slot, start: time }
            : { ...slot, end: time }
          : slot
      )
    );
  }

  saveException(): void {
    this.formControl.markAsTouched();
    this.onTouch();

    const selected = this.selectedDate();
    const dateKey = this.formatDateKey(selected);
    const overrideKey = `override_${dateKey}`;

    const updated = { ...this._availability() };
    if (this.exceptionCustomHours()) {
      const slots: TimeRange[] = this.exceptionSlots()
        .map((s) => ({ start: this.dateToTime(s.start), end: this.dateToTime(s.end) }))
        .filter((s) => s.start < s.end);
      updated[overrideKey] = slots;
    } else {
      updated[overrideKey] = [];
    }
    this._availability.set(updated);
    this.emitValue(updated);
    this.showExceptionDialog.set(false);
  }

  removeException(): void {
    this.formControl.markAsTouched();
    this.onTouch();

    const selected = this.selectedDate();
    const dateKey = this.formatDateKey(selected);
    const overrideKey = `override_${dateKey}`;

    const updated = { ...this._availability() };
    delete updated[overrideKey];
    this._availability.set(updated);
    this.emitValue(updated);
  }

  // Edit hours for selected date (creates/updates override)
  openEditHoursDialog(): void {
    const info = this.selectedDayInfo();
    const ranges =
      info.timeSlots.length > 0
        ? info.timeSlots
        : [{ start: '09:00', end: '17:00' }];

    const selected = this.selectedDate();
    const dateKey = this.formatDateKey(selected);
    const overrideKey = `override_${dateKey}`;

    this.editingDay.set(overrideKey);
    this.editingSlots.set(ranges.map((r) => ({ start: this.timeToDate(r.start), end: this.timeToDate(r.end) })));
    this.showTimeDialog.set(true);
  }

  // Preset management
  openPresetDialog(preset: 'monFri' | 'monSat' | 'custom'): void {
    if (preset === 'monFri') {
      this.presetDays.set(
        new Set(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'])
      );
      this.presetStartDate.set(this.timeToDate('09:00'));
      this.presetEndDate.set(this.timeToDate('17:00'));
    } else if (preset === 'monSat') {
      this.presetDays.set(
        new Set([
          'Monday',
          'Tuesday',
          'Wednesday',
          'Thursday',
          'Friday',
          'Saturday',
        ])
      );
      this.presetStartDate.set(this.timeToDate('08:00'));
      this.presetEndDate.set(this.timeToDate('16:00'));
    } else {
      this.presetDays.set(
        new Set(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'])
      );
      this.presetStartDate.set(this.timeToDate('09:00'));
      this.presetEndDate.set(this.timeToDate('17:00'));
    }
    this.showPresetDialog.set(true);
  }

  togglePresetDay(dayKey: string): void {
    this.presetDays.update((s) => {
      const newSet = new Set(s);
      if (newSet.has(dayKey)) {
        newSet.delete(dayKey);
      } else {
        newSet.add(dayKey);
      }
      return newSet;
    });
  }

  isPresetDayActive(dayKey: string): boolean {
    return this.presetDays().has(dayKey);
  }

  updatePresetStartDate(time: Date): void {
    this.presetStartDate.set(time);
  }

  updatePresetEndDate(time: Date): void {
    this.presetEndDate.set(time);
  }

  applyPreset(): void {
    this.formControl.markAsTouched();
    this.onTouch();

    const updated: Availability = {};
    const days = this.presetDays();
    const start = this.dateToTime(this.presetStartDate());
    const end = this.dateToTime(this.presetEndDate());

    // Preserve existing overrides
    const current = this._availability();
    for (const key of Object.keys(current)) {
      if (key.startsWith('override_')) {
        updated[key] = current[key];
      }
    }

    for (const day of DAYS) {
      if (days.has(day.key)) {
        updated[day.key] = [{ start, end }];
      }
    }

    this._availability.set(updated);
    this.emitValue(updated);
    this.showPresetDialog.set(false);
  }

  // Emit value stripped of internal override_ prefixed keys for the API
  private emitValue(value: Availability): void {
    const cleaned: Availability = {};
    for (const key of Object.keys(value)) {
      if (key.startsWith('override_')) {
        // Store date overrides with the plain date key (yyyy-MM-dd) for the API
        cleaned[key.replace('override_', '')] = value[key];
      } else {
        cleaned[key] = value[key];
      }
    }
    this.onChange(cleaned);
  }

  // Helpers
  timeToDate(time: string): Date {
    const parts = time.split(':');
    return new Date(2000, 0, 1, parseInt(parts[0] || '0', 10), parseInt(parts[1] || '0', 10), 0, 0);
  }

  dateToTime(d: Date): string {
    return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`;
  }

  formatDateKey(date: Date): string {
    const y = date.getFullYear();
    const m = (date.getMonth() + 1).toString().padStart(2, '0');
    const d = date.getDate().toString().padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

}
