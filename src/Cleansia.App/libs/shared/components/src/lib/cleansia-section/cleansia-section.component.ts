import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cleansia-cleansia-section',
  imports: [CommonModule],
  templateUrl: './cleansia-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaSectionComponent {}
