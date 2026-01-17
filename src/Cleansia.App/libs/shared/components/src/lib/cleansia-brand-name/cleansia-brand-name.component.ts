import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'cleansia-brand-name',
  templateUrl: './cleansia-brand-name.component.html',
  standalone: true,
  imports: [NgClass, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaBrandNameComponent {
  defaultRoute = input<string>('');
  showName = input<boolean>(true);
  wrapped = input<boolean>(false);
}
