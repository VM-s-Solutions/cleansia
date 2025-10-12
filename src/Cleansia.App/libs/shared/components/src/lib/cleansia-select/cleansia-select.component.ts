import { CommonModule } from '@angular/common';
import { Component, forwardRef, input, output } from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { FloatLabelModule } from 'primeng/floatlabel';
import { SelectModule } from 'primeng/select';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';
import { ICleansiaSelectOption } from './cleansia-select.models';

@Component({
  selector: 'cleansia-select',
  standalone: true,
  imports: [
    CommonModule,
    ErrorPipe,
    SelectModule,
    FormsModule,
    FloatLabelModule,
  ],
  templateUrl: './cleansia-select.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaSelectComponent),
      multi: true,
    },
  ],
})
export class CleansiaSelectComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(this.getDefaultLabelId());
  options = input<ICleansiaSelectOption[]>([]);
  floatVariant = input<'over' | 'in' | 'on'>('on');
  showClear = input(true);
  filter = input(false);
  filterBy = input<string>('label');

  valueChanges = output<any>();

  innerValue: any = null;

  override writeValue(value: any): void {
    this.innerValue = value ?? null;
  }

  handleChange(event: any): void {
    const value = event.value;
    this.innerValue = value;
    this.onChange(value);
    this.valueChanges.emit(value);
  }

  private getDefaultLabelId() {
    return 'cleansia-select-' + Math.random().toString(36).substring(2);
  }
}
