import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CleansiaButtonComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';

@Component({
  selector: 'cleansia-cta',
  templateUrl: './cta.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CleansiaButtonComponent, CleansiaTitleComponent],
})
export class CtaComponent {}
