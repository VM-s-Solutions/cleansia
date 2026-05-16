import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, forwardRef, input, output } from '@angular/core';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaSelectComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(this.getDefaultLabelId());
  options = input<ICleansiaSelectOption[]>([]);
  floatVariant = input<'over' | 'in' | 'on'>('on');
  showClear = input(true);
  filter = input(false);
  filterBy = input<string>('label');
  appendTo = input<'body' | null>(null);

  // TODO(W6.2): Tightening valueChanges to a stricter generic cascades
  // into many consumer template fixes (e.g. DocumentType, OrderStatus).
  // Left as `any` until consumers migrate; innerValue/writeValue tightened.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  valueChanges = output<any>();

  innerValue: unknown = null;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  override writeValue(value: any): void {
    this.innerValue = value ?? null;
  }

  handleChange(event: { value: unknown }): void {
    const value = event.value;
    this.innerValue = value;
    this.onChange(value);
    this.valueChanges.emit(value);
  }

  private getDefaultLabelId() {
    return 'cleansia-select-' + Math.random().toString(36).substring(2);
  }
}
