import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'cleansia-cleansia-section',
  imports: [CommonModule],
  templateUrl: './cleansia-section.component.html',
  styleUrl: './cleansia-section.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaSectionComponent {}
