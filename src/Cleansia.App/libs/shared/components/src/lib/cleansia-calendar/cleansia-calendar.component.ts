import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, forwardRef, input, output } from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { DatePickerModule } from 'primeng/datepicker';
import { FloatLabelModule } from 'primeng/floatlabel';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

@Component({
  selector: 'cleansia-calendar',
  standalone: true,
  imports: [
    CommonModule,
    ErrorPipe,
    DatePickerModule,
    FormsModule,
    FloatLabelModule,
  ],
  templateUrl: './cleansia-calendar.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaCalendarComponent),
      multi: true,
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaCalendarComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(this.getDefaultLabelId());
  floatVariant = input<'over' | 'in' | 'on'>('on');
  showIcon = input<boolean>(true);
  iconDisplay = input<'input' | 'button'>('input');
  dateFormat = input<string>('dd.mm.yy');
  minDate = input<Date | null>(null);
  maxDate = input<Date | null>(null);
  appendTo = input<'body' | null>(null);

  valueChanges = output<Date | null>();

  innerValue: Date | null = null;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  override writeValue(value: any): void {
    this.innerValue = value ?? null;
  }

  handleChange(event: Date | null): void {
    const value = event;
    this.innerValue = value ?? null;
    this.onChange(value);
    this.valueChanges.emit(value);
  }

  private getDefaultLabelId() {
    return 'cleansia-calendar-' + Math.random().toString(36).substring(2);
  }
}
