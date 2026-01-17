import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  input,
  TemplateRef,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { CleansiaButtonComponent } from '../cleansia-button';
import { CleansiaDynamicBackgroundComponent } from '../cleansia-dynamic-background';

@Component({
  selector: 'cleansia-not-found',
  templateUrl: './cleansia-not-found.component.html',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaDynamicBackgroundComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaNotFoundComponent {
  code = input<string>('404');
  title = input<string>();
  message = input<string>();
  showBackButtons = input<boolean>(true);
  customButtonTemplate = input<TemplateRef<unknown>>();

  get isHistoryAvailable(): boolean {
    return window.history.length > 1;
  }

  goBack(): void {
    if (window.history.length > 1) {
      window.history.back();
      return;
    }
    window.location.href = '/';
  }
}
