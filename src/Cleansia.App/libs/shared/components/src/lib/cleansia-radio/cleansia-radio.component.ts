import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { RadioButtonModule } from 'primeng/radiobutton';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

@Component({
  selector: 'cleansia-radio',
  templateUrl: './cleansia-radio.component.html',
  standalone: true,
  imports: [ErrorPipe, CommonModule, RadioButtonModule, FormsModule],
})
export class CleansiaRadioComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(
    'cleansia-radio-' + Math.random().toString(36).substring(2)
  );
  value = input<any>(); // The value this radio button represents
  name = input<string>(''); // Radio group name
  readonly = input<boolean>(false);

  valueChanges = output<any>();

  innerValue: any = null;

  override writeValue(value: any): void {
    this.innerValue = value;
  }

  handleChange(value: any): void {
    this.innerValue = value;
    this.onChange(value);
    this.valueChanges.emit(value);
  }
}
