import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-brand-name',
  templateUrl: './cleansia-brand-name.component.html',
  standalone: true,
  imports: [RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaBrandNameComponent {
  defaultRoute = input<string>('');
  compact = input<boolean>(false);
}
