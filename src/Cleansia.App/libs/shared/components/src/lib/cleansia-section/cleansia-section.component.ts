import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'cleansia-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cleansia-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaSectionComponent {
  title = input<string>('');
}
