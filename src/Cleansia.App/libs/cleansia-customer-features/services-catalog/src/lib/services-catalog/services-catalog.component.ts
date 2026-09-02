import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent, CleansiaScrollTopComponent } from '@cleansia/components';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import {
  loadCustomerPackages,
  loadCustomerServices,
  selectCustomerCatalogLoading,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { PackageListItem, ServiceListItem } from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Skeleton } from 'primeng/skeleton';

type SortOption = 'price_asc' | 'price_desc' | 'name_asc';

@Component({
  selector: 'cleansia-customer-services-catalog',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaScrollTopComponent,
    FoamEdgeComponent,
    Skeleton,
  ],
  templateUrl: './services-catalog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServicesCatalogComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  /** The artboard shows three packages and a link to the rest. */
  private readonly PACKAGE_PREVIEW = 3;

  services = toSignal(this.store.select(selectCustomerServices), { initialValue: [] });
  packages = toSignal(this.store.select(selectCustomerPackages), { initialValue: [] });
  loading = toSignal(this.store.select(selectCustomerCatalogLoading), { initialValue: false });

  /**
   * Sorting is offered over the services and NOT over the packages, which is
   * the artboard's split rather than an omission. The packages section shows a
   * curated three with a reveal link; sorting three cards by price is a control
   * that changes nothing a reader can see.
   */
  serviceSort = signal<SortOption>('price_asc');
  showAllPackages = signal(false);

  readonly sortOptions: { value: SortOption; labelKey: string }[] = [
    { value: 'price_asc', labelKey: 'pages.services.sort_price_asc' },
    { value: 'price_desc', labelKey: 'pages.services.sort_price_desc' },
    { value: 'name_asc', labelKey: 'pages.services.sort_name_asc' },
  ];

  sortedServices = computed(() => this.sortItems([...this.services()], this.serviceSort(), 'basePrice'));

  /** Cheapest first, so the preview opens on the least committing option. */
  private readonly packagesByPrice = computed(() =>
    [...this.packages()].sort((a, b) => (a.price ?? 0) - (b.price ?? 0))
  );

  /**
   * The three the section leads with, arranged so the recommended one is the
   * middle card. The inverted card is the design's anchor — at the end of the
   * row it reads as the most expensive option rather than the suggested one.
   */
  visiblePackages = computed(() => {
    const all = this.packagesByPrice();
    if (this.showAllPackages() || all.length <= this.PACKAGE_PREVIEW) return all;

    const preview = all.slice(0, this.PACKAGE_PREVIEW);
    const popularAt = preview.findIndex(p => p.isPopular);
    const middle = Math.floor(this.PACKAGE_PREVIEW / 2);
    if (popularAt < 0 || popularAt === middle) return preview;

    const reordered = [...preview];
    [reordered[popularAt], reordered[middle]] = [reordered[middle], reordered[popularAt]];
    return reordered;
  });

  hasMorePackages = computed(
    () => !this.showAllPackages() && this.packagesByPrice().length > this.PACKAGE_PREVIEW
  );

  /**
   * The mascot poses the service cards cycle through. All eleven are cut to one
   * 340x280 canvas at an identical character height on a shared baseline, so a
   * single CSS height renders them at the same size — the raw drawings differ
   * enough in content bounds that `object-fit: contain` alone scales them
   * unequally, which is why only the normalised tiles are listed here.
   * -> home services.component.ts, which established the canvas.
   */
  private readonly mascotTiles = [
    'assets/images/mascot/mascot-mopping-tile.webp',
    'assets/images/mascot/mascot-idea-tile.webp',
    'assets/images/mascot/mascot-vacuuming-tile.webp',
    'assets/images/mascot/mascot-thumbs-up-tile.webp',
    'assets/images/mascot/mascot-dusting-tile.webp',
    'assets/images/mascot/mascot-leaning-tile.webp',
    'assets/images/mascot/mascot-spray-and-cloth-tile.webp',
    'assets/images/mascot/mascot-cleaning-tile.webp',
  ];

  private readonly packageMascots = [
    'assets/images/mascot/mascot-ready-tile.webp',
    'assets/images/mascot/mascot-floor-scrubber-tile.webp',
    'assets/images/mascot/mascot-arms-crossed-tile.webp',
  ];

  readonly heroMascotLeft = 'assets/images/mascot/mascot-leaning-tile.webp';
  readonly heroMascotRight = 'assets/images/mascot/mascot-dusting-tile.webp';
  readonly closingMascot = 'assets/images/mascot/mascot-waving.webp';

  ngOnInit(): void {
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
  }

  serviceMascot(index: number): string {
    return this.mascotTiles[index % this.mascotTiles.length];
  }

  packageMascot(index: number): string {
    return this.packageMascots[index % this.packageMascots.length];
  }

  getTranslation(item: ServiceListItem | PackageListItem, field: string): string {
    const lang = this.translate.currentLang || this.translate.getDefaultLang();
    const translations = item.translations;
    if (translations && translations[lang]) {
      const translated = (translations[lang] as unknown as Record<string, string>)[field];
      if (translated) return translated;
    }
    return (item as unknown as Record<string, string>)[field] || '';
  }

  formatPrice(price: number): string {
    return new Intl.NumberFormat('cs-CZ', {
      style: 'currency',
      currency: 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }

  /**
   * A service priced per room has no single number to show, so the card states
   * the base as a floor and puts the rate beside it. The artboard's per-service
   * units ("za okno", "za kus", "na míru") are sample copy — the catalogue
   * carries a base price and a per-room rate and nothing that could produce
   * them, and inventing a unit column to say "per window" would be a schema
   * change bought for one label.
   */
  isPricedPerRoom(service: ServiceListItem): boolean {
    return (service.perRoomPrice ?? 0) > 0;
  }

  getIncludedServiceNames(pkg: PackageListItem): string[] {
    if (!pkg.includedServices?.length) return [];
    const lang = this.translate.currentLang || this.translate.getDefaultLang();
    return pkg.includedServices
      .map(svc => {
        const t = svc.translations?.[lang] as unknown as Record<string, string> | undefined;
        return t?.['name'] || svc.name || '';
      })
      .filter(n => !!n);
  }

  onServiceSortChange(sort: SortOption): void {
    this.serviceSort.set(sort);
  }

  revealAllPackages(): void {
    this.showAllPackages.set(true);
  }

  bookPackage(pkg: PackageListItem): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER], { queryParams: { packageId: pkg.id } });
  }

  bookService(service: ServiceListItem): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER], { queryParams: { serviceId: service.id } });
  }

  bookNow(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER]);
  }

  /** The price calculator lives in the home page's hero. */
  openCalculator(): void {
    this.router.navigate(['/'], { fragment: 'hero' });
  }

  private sortItems<T>(items: T[], sort: SortOption, priceField: string): T[] {
    return items.sort((a, b) => {
      switch (sort) {
        case 'price_asc':
          return ((a as Record<string, number>)[priceField] ?? 0) - ((b as Record<string, number>)[priceField] ?? 0);
        case 'price_desc':
          return ((b as Record<string, number>)[priceField] ?? 0) - ((a as Record<string, number>)[priceField] ?? 0);
        case 'name_asc':
          return this.getTranslation(a as ServiceListItem, 'name').localeCompare(
            this.getTranslation(b as ServiceListItem, 'name')
          );
        default:
          return 0;
      }
    });
  }
}
