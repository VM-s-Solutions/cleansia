import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { CheckboxModule } from 'primeng/checkbox';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

@Component({
  selector: 'cleansia-checkbox',
  templateUrl: './cleansia-checkbox.component.html',
  standalone: true,
  imports: [ErrorPipe, CommonModule, CheckboxModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaCheckboxComponent extends CleansiaBaseFormInputComponent {
  binary = input<boolean>(true); // Default to binary mode
  id = input<string>(
    'cleansia-checkbox-' + Math.random().toString(36).substring(2)
  );
  readonly = input<boolean>(false);

  valueChanges = output<boolean>();

  innerValue = false;

  override writeValue(value: boolean): void {
    this.innerValue = value;
  }

  handleChange(value: boolean): void {
    this.innerValue = value;
    this.onChange(value);
    this.valueChanges.emit(value);
  }
}
