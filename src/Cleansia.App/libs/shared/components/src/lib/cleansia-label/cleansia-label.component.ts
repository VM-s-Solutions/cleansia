import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type LabelSize = 'xs' | 'sm' | 'base' | 'lg' | 'xl';
type LabelWeight = 'normal' | 'medium' | 'semibold' | 'bold';
type LabelColor = 'primary' | 'secondary' | 'success' | 'danger' | 'warning' | 'muted';

@Component({
  selector: 'cleansia-label',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cleansia-label.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaLabelComponent {
  text = input<string>('');
  size = input<LabelSize>('base');
  weight = input<LabelWeight>('normal');
  color = input<LabelColor>('primary');

  get classNames(): string {
    return `cleansia-label cleansia-label--${this.size()} cleansia-label--${this.weight()} cleansia-label--${this.color()}`;
  }
}
