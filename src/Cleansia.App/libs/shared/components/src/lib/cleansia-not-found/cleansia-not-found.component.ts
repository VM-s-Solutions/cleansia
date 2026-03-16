import { isPlatformBrowser, NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  PLATFORM_ID,
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
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  code = input<string>('404');
  title = input<string>();
  message = input<string>();
  mascotSrc = input<string>('assets/images/mascot/mascot-waving.png');
  showBackButtons = input<boolean>(true);
  customButtonTemplate = input<TemplateRef<unknown>>();

  get isHistoryAvailable(): boolean {
    if (!this.isBrowser) return false;
    return window.history.length > 1;
  }

  goBack(): void {
    if (!this.isBrowser) return;
    if (window.history.length > 1) {
      window.history.back();
      return;
    }
    window.location.href = '/';
  }
}
