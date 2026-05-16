import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-partner-gdpr',
  standalone: true,
  imports: [
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
    RouterModule,
  ],
  templateUrl: './gdpr.component.html',
  styleUrl: './gdpr.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartnerGdprComponent {}
