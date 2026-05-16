import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import { RadioButtonModule } from 'primeng/radiobutton';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

@Component({
  selector: 'cleansia-radio',
  templateUrl: './cleansia-radio.component.html',
  standalone: true,
  imports: [ErrorPipe, CommonModule, RadioButtonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaRadioComponent extends CleansiaBaseFormInputComponent {
  id = input<string>(
    'cleansia-radio-' + Math.random().toString(36).substring(2)
  );
  // TODO(W6.2): value/valueChanges left as `any` because consumers in
  // pay-period-management, employee-management bind concrete enum types.
  // Tightening cascades into many consumer fixes; innerValue tightened.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  value = input<any>(); // The value this radio button represents
  name = input<string>(''); // Radio group name
  readonly = input<boolean>(false);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  valueChanges = output<any>();

  innerValue: unknown = null;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  override writeValue(value: any): void {
    this.innerValue = value;
  }

  handleChange(value: unknown): void {
    this.innerValue = value;
    this.onChange(value);
    this.valueChanges.emit(value);
  }
}
