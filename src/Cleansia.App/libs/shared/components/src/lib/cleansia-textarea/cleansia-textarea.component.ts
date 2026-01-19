import { CommonModule } from '@angular/common';
import { Component, forwardRef, input, output } from '@angular/core';
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
})
export class CleansiaTextareaComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(this.getDefaultLabelId());
  rows = input<number>(3);
  cols = input<number | undefined>(undefined);
  autoResize = input<boolean>(false);
  floatVariant = input<'over' | 'in' | 'on'>('on');

  valueChanges = output<any>();

  innerValue = '';

  override writeValue(value: string): void {
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
