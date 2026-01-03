import { CommonModule } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule, ButtonSeverity } from 'primeng/button';
import { InputSize } from '../cleansia-base-form/cleansia-base-form.models';

@Component({
  selector: 'cleansia-button',
  standalone: true,
  imports: [CommonModule, ButtonModule, TranslateModule],
  templateUrl: './cleansia-button.component.html',
})
export class CleansiaButtonComponent {
  buttonType = input<'button' | 'submit' | 'reset'>('button');
  style = input<'basic-button' | 'raised-button'>('basic-button');
  severity = input<ButtonSeverity>('info');
  title = input<string>('');
  label = input<string>(''); // Alias for title to match PrimeNG API
  size = input<InputSize>('full-width');
  icon = input<string | undefined>(undefined);
  iconPosition = input<'left' | 'right'>('left');
  iconOutlined = input<boolean>(false);
  rounded = input<boolean>(false);
  outlined = input<boolean>(false); // PrimeNG-compatible outlined property
  disabled = input<boolean>(false);
  loading = input<boolean>(false);
  className = input<string>('');

  onClick = output<MouseEvent>(); // PrimeNG-compatible output name
  clickFn = output<MouseEvent>(); // Legacy output name

  handleClick(event: MouseEvent): void {
    this.clickFn.emit(event);
    this.onClick.emit(event);
  }
}
