import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import {
  loadCustomerServices,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { PackageListItem, ServiceListItem } from '@cleansia/customer-services';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
@Component({
  selector: 'cleansia-services',
  templateUrl: './services.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, TranslatePipe],
})
export class ServicesComponent {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Bumped on every language change.
   *
   * Service names and descriptions come from the catalogue's own per-language
   * dictionary, not from ngx-translate, so nothing in the template depends on a
   * pipe — and an OnPush component with no changed input never re-rendered. The
   * cards kept the language they were first drawn in.
   */
  private readonly lang = signal(this.translate.currentLang);

  constructor() {
    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ lang }) => this.lang.set(lang));
  }

  services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });

  /**
   * The first three services are cards; everything after them is a chip.
   *
   * The split is the design's, and it also means the section grows with the
   * catalogue rather than naming three services in the markup — the copy says
   * "eleven services", and that number should come from the catalogue.
   */
  readonly cardServices = computed(() => {
    this.lang();
    return this.services().slice(0, 3);
  });

  readonly chipServices = computed(() => {
    this.lang();
    return this.services().slice(3);
  });

  fallbackServices = [
    { name: 'pages.home.fallback_services.s1.name', desc: 'pages.home.fallback_services.s1.desc', price: 890 },
    { name: 'pages.home.fallback_services.s2.name', desc: 'pages.home.fallback_services.s2.desc', price: 1690 },
    { name: 'pages.home.fallback_services.s3.name', desc: 'pages.home.fallback_services.s3.desc', price: 640 },
  ];

  getTranslation(item: ServiceListItem | PackageListItem, field: string): string {
    const lang = this.lang() || this.translate.getDefaultLang();
    const translations = item.translations;
    if (translations && translations[lang]) {
      const translated = (translations[lang] as unknown as Record<string, string>)[field];
      if (translated) return translated;
    }
    return (item as unknown as Record<string, string>)[field] || '';
  }

  formatPrice(price: number | undefined): string {
    if (price == null) return '';
    return new Intl.NumberFormat('cs-CZ', {
      style: 'currency',
      currency: 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }

  /**
   * The three service cards each show a different mascot pose. The artwork is
   * pre-normalised to one 340x280 canvas at an identical character height and a
   * shared baseline, so equal CSS sizing renders equal characters - the raw
   * drawings differ enough in content bounds (281x291 / 299x381 / 232x295) that
   * `object-fit: contain` alone scales them unequally.
   */
  private readonly mascotTiles = [
    'assets/images/mascot/mascot-vacuuming-tile.webp',
    'assets/images/mascot/mascot-dusting-tile.webp',
    'assets/images/mascot/mascot-spray-and-cloth-tile.webp',
  ];

  mascotTile(index: number): string {
    return this.mascotTiles[index % this.mascotTiles.length];
  }
}
