import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'cleansia-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cleansia-section.component.html',
  styleUrls: ['./cleansia-section.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaSectionComponent {
  title = input<string>('');
}
