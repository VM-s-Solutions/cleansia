import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-title',
  templateUrl: './cleansia-title.component.html',
  standalone: true,
  imports: [TranslatePipe],
})
export class CleansiaTitleComponent {
  title = input.required<string>();
  size = input<'small' | 'default' | 'big' | 'large'>('default');
  className = input<string>();
}
