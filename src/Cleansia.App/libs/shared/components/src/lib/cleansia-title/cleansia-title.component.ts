import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

export type CleansiaTitleSize = 'small' | 'default' | 'big' | 'large';
export type CleansiaTitleLevel = 1 | 2 | 3 | 5;

const LEVEL_FOR_SIZE: Record<CleansiaTitleSize, CleansiaTitleLevel> = {
  large: 1,
  big: 2,
  default: 3,
  small: 5,
};

@Component({
  selector: 'cleansia-title',
  templateUrl: './cleansia-title.component.html',
  standalone: true,
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaTitleComponent {
  title = input.required<string>();
  size = input<CleansiaTitleSize>('default');
  className = input<string>();

  // Heading rank is an outline decision, not a type-scale one: a page's own
  // title is the h1 whatever size it is drawn at.
  level = input<CleansiaTitleLevel>();

  headingLevel = computed(() => this.level() ?? LEVEL_FOR_SIZE[this.size()]);
}
