import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CleansiaTitleComponent,
} from '@cleansia/components';

@Component({
  selector: 'cleansia-features',
  templateUrl: './features.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CleansiaTitleComponent],
})
export class FeaturesComponent {}
