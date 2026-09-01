import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { FoamEdgeComponent } from '../foam-edge/foam-edge.component';
import { QuickQuoteComponent, QuickQuoteService } from '../quick-quote/quick-quote.component';
import { Store } from '@ngrx/store';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { selectCustomerServices } from '@cleansia/customer-stores';
import { ServiceListItem } from '@cleansia/customer-services';
import {
  CleansiaButtonComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';

const HERO_IMAGE = 'assets/images/mascot/mascot-mopping.webp';
const PRELOAD_ID = 'cl-hero-img-preload';

@Component({
  selector: 'cleansia-hero',
  templateUrl: './hero.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FoamEdgeComponent, QuickQuoteComponent, TranslatePipe, CleansiaButtonComponent, CleansiaTitleComponent, RouterModule],
})
export class HeroComponent {
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  // Declared before `lang`, which reads it at field-initialiser time.
  private readonly translate = inject(TranslateService);

  /**
   * Bumped on every language change.
   *
   * The calculator's chips come from the catalogue's own per-language
   * translations, not from a TranslatePipe, and `translate.currentLang` is a
   * plain property — so `quoteServices` never re-ran and the chips kept the
   * language the page was first drawn in while the rest of it switched.
   */
  private readonly lang = signal(this.translate.currentLang);

  constructor() {
    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ lang }) => this.lang.set(lang));

    // Preload the LCP hero image from <head>. Running this during SSR puts
    // the hint into the served HTML, so the browser fetches the image ahead
    // of the script bundles instead of competing with them.
    if (!this.document.getElementById(PRELOAD_ID)) {
      const link = this.document.createElement('link');
      link.id = PRELOAD_ID;
      link.rel = 'preload';
      link.setAttribute('as', 'image');
      link.href = HERO_IMAGE;
      // Mirror the <img srcset/sizes> so the preload fetches the same
      // variant the responsive image will pick.
      link.setAttribute(
        'imagesrcset',
        'assets/images/mascot/mascot-mopping-480.webp 480w, assets/images/mascot/mascot-mopping.webp 800w'
      );
      link.setAttribute('imagesizes', '(max-width: 768px) 260px, 400px');
      link.setAttribute('fetchpriority', 'high');
      this.document.head.appendChild(link);
    }
  }

  private readonly store = inject(Store);

  private readonly catalogue = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });

  /**
   * The first few services, flattened for the calculator.
   *
   * Capped at five: the chip row is one line in the hero card, and a visitor
   * choosing between eleven options above the fold is being asked to browse
   * rather than to price.
   */
  readonly quoteServices = computed<QuickQuoteService[]>(() => {
    this.lang(); // re-run when the language changes
    return this.catalogue()
      .slice(0, 5)
      .map((service) => ({ id: service.id ?? '', name: this.serviceName(service) }));
  });

  private serviceName(service: ServiceListItem): string {
    const lang = this.lang() || this.translate.getDefaultLang();
    const translated = service.translations?.[lang] as unknown as Record<string, string> | undefined;
    return translated?.['name'] || service.name || '';
  }
}
