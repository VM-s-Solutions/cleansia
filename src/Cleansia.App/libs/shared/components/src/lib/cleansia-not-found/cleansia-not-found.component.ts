import { isPlatformBrowser, NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  PLATFORM_ID,
  TemplateRef,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * One way out of a dead end: a translation key and the route it opens.
 *
 * `fragment` is here because not every destination is a route — the customer
 * app's "how it works" is a section of the home page — and a 404 that silently
 * drops a link because it is an anchor is a 404 with one fewer way out.
 */
export interface NotFoundLink {
  readonly labelKey: string;
  readonly route: string;
  readonly fragment?: string;
}

@Component({
  selector: 'cleansia-not-found',
  templateUrl: './cleansia-not-found.component.html',
  standalone: true,
  imports: [NgTemplateOutlet, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaNotFoundComponent {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  code = input<string>('404');
  title = input<string>();
  message = input<string>();
  // `mascot-waving.png` is the ONLY mascot the partner and admin apps ship —
  // the other 32 live in the customer app's assets alone. A default naming one
  // of those renders the shared 404 with a 200px hole on two of the three apps.
  // The customer app passes its own. → CustomerNotFoundComponent
  mascotSrc = input<string>('assets/images/mascot/mascot-waving.png');
  showBackButtons = input<boolean>(true);
  customButtonTemplate = input<TemplateRef<unknown>>();

  /**
   * Ways on from here, supplied by the HOST APP.
   *
   * This component renders on the customer, partner and admin apps, so the
   * links cannot be written into it — "Cleansia Plus" is nonsense on two of
   * them. Empty by default: an app that says nothing shows nothing, and still
   * gets the home and back actions.
   */
  links = input<readonly NotFoundLink[]>([]);

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
