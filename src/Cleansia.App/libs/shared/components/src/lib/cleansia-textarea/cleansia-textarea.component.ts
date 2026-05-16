import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, forwardRef, input, output } from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { FloatLabelModule } from 'primeng/floatlabel';
import { Textarea } from 'primeng/textarea';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

@Component({
  selector: 'cleansia-textarea',
  standalone: true,
  imports: [CommonModule, ErrorPipe, Textarea, FormsModule, FloatLabelModule],
  templateUrl: './cleansia-textarea.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaTextareaComponent),
      multi: true,
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaTextareaComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(this.getDefaultLabelId());
  rows = input<number>(3);
  cols = input<number | undefined>(undefined);
  autoResize = input<boolean>(false);
  floatVariant = input<'over' | 'in' | 'on'>('on');

  valueChanges = output<string>();

  innerValue = '';

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  override writeValue(value: any): void {
    this.innerValue = value ?? '';
  }

  handleChange(event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.innerValue = value ?? '';
    this.onChange(value);
    this.valueChanges.emit(value);
  }

  private getDefaultLabelId() {
    return 'cleansia-textarea-' + Math.random().toString(36).substring(2);
  }
}
