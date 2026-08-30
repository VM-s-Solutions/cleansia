import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CleansiaTitleComponent,
} from '@cleansia/components';

@Component({
  selector: 'cleansia-benefits',
  templateUrl: './benefits.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CleansiaTitleComponent],
})
export class BenefitsComponent {}
