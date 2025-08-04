import { Component, input } from '@angular/core';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'cleansia-button',
  templateUrl: './cleansia-button.component.html',
  standalone: true,
  imports: [ButtonModule],
})
export class CleansiaButtonComponent {
  icon = input<string>('');
  label = input<string>('');
  severity = input<'primary' | 'secondary'>('primary');
}
