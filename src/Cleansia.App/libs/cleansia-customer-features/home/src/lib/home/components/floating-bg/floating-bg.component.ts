import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cleansia-floating-bg',
  templateUrl: './floating-bg.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FloatingBgComponent {}
