import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import {
  CleansiaButtonComponent,
} from '@cleansia/components';

@Component({
  selector: 'cleansia-cta',
  templateUrl: './cta.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule, TranslatePipe, ButtonModule, CleansiaButtonComponent],
})
export class CtaComponent {}
