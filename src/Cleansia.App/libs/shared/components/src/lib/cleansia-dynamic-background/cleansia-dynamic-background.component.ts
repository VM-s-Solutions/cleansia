import { LowerCasePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { ReplacePipe } from '@cleansia/pipes';

@Component({
  selector: 'cleansia-dynamic-background',
  templateUrl: './cleansia-dynamic-background.component.html',
  standalone: true,
  imports: [LowerCasePipe, ReplacePipe],
})
export class CleansiaDynamicBackgroundComponent {
  animationDuration = input(18);
  showIcons = input(true);

  icons: string[] = [
    'fa-broom',
    'fa-soap',
    'fa-spray-can-sparkles',
    'fa-vacuum',
    'fa-bucket',
    'fa-hands-bubbles',
    'fa-shower',
    'fa-trash-can',
  ];
}
