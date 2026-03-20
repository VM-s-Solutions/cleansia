import { CommonModule } from '@angular/common';
import { Component, computed, forwardRef, input, output, signal } from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { FloatLabelModule } from 'primeng/floatlabel';
import { InputTextModule } from 'primeng/inputtext';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

@Component({
  selector: 'cleansia-text-input',
  standalone: true,
  imports: [
    CommonModule,
    ErrorPipe,
    InputTextModule,
    FormsModule,
    FloatLabelModule,
  ],
  templateUrl: './cleansia-text-input.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaTextInputComponent),
      multi: true,
    },
  ],
})
export class CleansiaTextInputComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(this.getDefaultLabelId());
  dataType = input<'text' | 'password' | 'email' | 'number'>('text');
  floatVariant = input<'over' | 'in' | 'on'>('on');

  valueChanges = output<any>();

  passwordVisible = signal(false);
  effectiveType = computed(() =>
    this.dataType() === 'password' && this.passwordVisible() ? 'text' : this.dataType()
  );

  togglePasswordVisibility(): void {
    this.passwordVisible.update((v) => !v);
  }

  innerValue = '';

  override writeValue(value: string): void {
    this.innerValue = value ?? '';
  }

  handleChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.innerValue = value ?? '';
    this.onChange(value);
    this.valueChanges.emit(value);
  }

  private getDefaultLabelId() {
    return 'cleansia-input-' + Math.random().toString(36).substring(2);
  }
}
