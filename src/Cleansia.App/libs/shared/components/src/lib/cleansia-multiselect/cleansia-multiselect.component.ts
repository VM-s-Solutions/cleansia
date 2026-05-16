import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, forwardRef, input, output } from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { FloatLabelModule } from 'primeng/floatlabel';
import { MultiSelectModule } from 'primeng/multiselect';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';
import { ICleansiaSelectOption } from '../cleansia-select/cleansia-select.models';

@Component({
  selector: 'cleansia-multiselect',
  standalone: true,
  imports: [
    CommonModule,
    ErrorPipe,
    MultiSelectModule,
    FormsModule,
    FloatLabelModule,
  ],
  templateUrl: './cleansia-multiselect.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaMultiselectComponent),
      multi: true,
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaMultiselectComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(this.getDefaultLabelId());
  options = input<ICleansiaSelectOption[]>([]);
  floatVariant = input<'over' | 'in' | 'on'>('on');
  showClear = input(true);
  filter = input(true);
  filterBy = input<string>('label');
  display = input<'comma' | 'chip'>('comma');
  maxSelectedLabels = input<number>(3);
  appendTo = input<'body' | null>(null);

  valueChanges = output<unknown[]>();

  innerValue: unknown[] = [];

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  override writeValue(value: any): void {
    this.innerValue = value ?? [];
  }

  handleChange(event: { value: unknown[] }): void {
    const value = event.value;
    this.innerValue = value ?? [];
    this.onChange(value);
    this.valueChanges.emit(value);
  }

  private getDefaultLabelId() {
    return 'cleansia-multiselect-' + Math.random().toString(36).substring(2);
  }
}
