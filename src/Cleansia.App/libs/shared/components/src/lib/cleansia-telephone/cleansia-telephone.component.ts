import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { FloatLabelModule } from 'primeng/floatlabel';
import { InputMaskModule } from 'primeng/inputmask';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

@Component({
  selector: 'cleansia-telephone',
  templateUrl: './cleansia-telephone.component.html',
  standalone: true,
  imports: [
    ErrorPipe,
    FormsModule,
    CommonModule,
    InputMaskModule,
    FloatLabelModule,
  ],
})
export class CleansiaTelephoneComponent extends CleansiaBaseFormInputComponent {
  floatVariant = input<'on' | 'in' | 'over'>('on');
  id = input<string>('cleansia-tel-' + Math.random().toString(36).substring(2));
  mask = input<string>('+420 999 999 999');

  valueChanges = output<string>();

  innerValue = '';

  override writeValue(value: string): void {
    this.innerValue = value;
  }

  handleChange(value: string): void {
    this.innerValue = value;
    this.onChange(value);
    this.valueChanges.emit(value);
  }

  getPlaceholder(): string {
    // Generate placeholder based on mask, replacing digits/letters with '_'
    return this.mask().replace(/[9a*]/g, '_');
  }

  getErrorMessage(): string {
    if (this.formControl.errors?.['required']) return 'This field is required.';
    return 'Invalid phone number.';
  }
}
