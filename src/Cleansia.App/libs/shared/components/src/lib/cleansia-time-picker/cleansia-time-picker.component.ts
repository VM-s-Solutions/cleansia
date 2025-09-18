import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  forwardRef,
  input,
  output,
  signal,
} from '@angular/core';
import {
  ControlValueAccessor,
  FormsModule,
  NG_VALUE_ACCESSOR,
  ReactiveFormsModule,
} from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { FloatLabelModule } from 'primeng/floatlabel';
import { InputTextModule } from 'primeng/inputtext';

@Component({
  selector: 'cleansia-time-picker',
  standalone: true,
  imports: [
    FormsModule,
    CommonModule,
    TranslatePipe,
    CalendarModule,
    DropdownModule,
    InputTextModule,
    FloatLabelModule,
    ReactiveFormsModule,
  ],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaTimePickerComponent),
      multi: true,
    },
  ],
  templateUrl: './cleansia-time-picker.component.html',
})
export class CleansiaTimePickerComponent implements ControlValueAccessor {
  // Inputs
  label = input<string>('');
  placeholder = input<string>('Select time');
  required = input<boolean>(false);
  disabled = input<boolean>(false);
  showErrors = input<boolean>(false);
  floatVariant = input<'auto' | 'always' | 'never' | 'on'>('auto');
  hourFormat = input<'12' | '24'>('24');
  stepMinute = input<number>(15);
  showIcon = input<boolean>(true);
  timeFormat = input<string>('HH:mm');

  // Outputs
  timeChange = output<string>();

  // Internal state
  internalValue = signal<Date | null>(null);

  private touched = signal(false);
  private onChange = (value: string) => {};
  private onTouched = () => {};

  // Computed properties
  timeValue = computed(() => {
    const date = this.internalValue();
    if (!date) return '';

    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    return `${hours}:${minutes}`;
  });

  // Effect to emit changes
  constructor() {
    effect(() => {
      const timeStr = this.timeValue();
      if (timeStr && this.touched()) {
        this.timeChange.emit(timeStr);
      }
    });
  }

  // ControlValueAccessor methods
  writeValue(value: string | Date | null): void {
    if (value === null || value === undefined || value === '') {
      this.internalValue.set(null);
      return;
    }

    if (value instanceof Date) {
      this.internalValue.set(value);
    } else if (typeof value === 'string') {
      // Parse time string (HH:mm format)
      const [hours, minutes] = value.split(':').map(Number);
      if (!isNaN(hours) && !isNaN(minutes)) {
        const date = new Date();
        date.setHours(hours, minutes, 0, 0);
        this.internalValue.set(date);
      }
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    // Handle disabled state if needed
  }

  // Event handlers
  onTimeChange(event: any): void {
    this.touched.set(true);
    this.onTouched();

    const date = event;
    this.internalValue.set(date);

    if (date) {
      const timeStr = this.timeValue();
      this.onChange(timeStr);
    } else {
      this.onChange('');
    }
  }

  onBlur(): void {
    this.touched.set(true);
    this.onTouched();
  }

  // Helper methods
  get isRequired(): boolean {
    return this.required();
  }

  get isDisabled(): boolean {
    return this.disabled();
  }

  get shouldShowErrors(): boolean {
    return this.showErrors() && this.touched();
  }
}
