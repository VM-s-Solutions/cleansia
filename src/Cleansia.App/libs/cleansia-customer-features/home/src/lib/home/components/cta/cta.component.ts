import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { FoamEdgeComponent } from '../foam-edge/foam-edge.component';

@Component({
  selector: 'cleansia-cta',
  templateUrl: './cta.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule, TranslatePipe, FoamEdgeComponent],
})
export class CtaComponent {}
