import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent, CleansiaScrollTopComponent } from '@cleansia/components';
import {
  loadCustomerPackages,
  loadCustomerServices,
  selectCustomerCatalogLoading,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { PackageListItem, PackageServiceSummary, ServiceListItem } from '@cleansia/partner-services';
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
    Skeleton,
  ],
  templateUrl: './services-catalog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServicesCatalogComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  private readonly INITIAL_VISIBLE = 3;

  // Store data
  services = toSignal(this.store.select(selectCustomerServices), { initialValue: [] });
  packages = toSignal(this.store.select(selectCustomerPackages), { initialValue: [] });
  loading = toSignal(this.store.select(selectCustomerCatalogLoading), { initialValue: false });

  // Sort state
  packageSort = signal<SortOption>('price_asc');
  serviceSort = signal<SortOption>('price_asc');

  // Expand state
  showAllPackages = signal(false);
  showAllServices = signal(false);

  // Sort options for template
  readonly sortOptions: { value: SortOption; labelKey: string }[] = [
    { value: 'price_asc', labelKey: 'pages.services.sort_price_asc' },
    { value: 'price_desc', labelKey: 'pages.services.sort_price_desc' },
    { value: 'name_asc', labelKey: 'pages.services.sort_name_asc' },
  ];

  // Sorted data
  sortedPackages = computed(() => {
    const pkgs = [...this.packages()];
    return this.sortItems(pkgs, this.packageSort(), 'price');
  });

  sortedServices = computed(() => {
    const svcs = [...this.services()];
    return this.sortItems(svcs, this.serviceSort(), 'basePrice');
  });

  // Visible items (first N)
  visiblePackages = computed(() => {
    const all = this.sortedPackages();
    if (this.showAllPackages() || all.length <= this.INITIAL_VISIBLE) return all;
    return all.slice(0, this.INITIAL_VISIBLE);
  });

  visibleServices = computed(() => {
    const all = this.sortedServices();
    if (this.showAllServices() || all.length <= this.INITIAL_VISIBLE) return all;
    return all.slice(0, this.INITIAL_VISIBLE);
  });

  // Teaser items (next 3 beyond visible, for fade preview)
  teaserPackages = computed(() => {
    const all = this.sortedPackages();
    if (this.showAllPackages() || all.length <= this.INITIAL_VISIBLE) return [];
    return all.slice(this.INITIAL_VISIBLE, this.INITIAL_VISIBLE + 3);
  });

  teaserServices = computed(() => {
    const all = this.sortedServices();
    if (this.showAllServices() || all.length <= this.INITIAL_VISIBLE) return [];
    return all.slice(this.INITIAL_VISIBLE, this.INITIAL_VISIBLE + 3);
  });

  // Whether "View All" button should show
  hasMorePackages = computed(() =>
    !this.showAllPackages() && this.sortedPackages().length > this.INITIAL_VISIBLE
  );

  hasMoreServices = computed(() =>
    !this.showAllServices() && this.sortedServices().length > this.INITIAL_VISIBLE
  );

  // Per-tier feature i18n keys
  private readonly packageFeatureKeys: Record<number, string[]> = {
    0: [
      'pages.services.features.basic_supplies',
      'pages.services.features.basic_cleaner',
      'pages.services.features.basic_cleaning',
      'pages.services.features.basic_sessions',
    ],
    1: [
      'pages.services.features.standard_supplies',
      'pages.services.features.standard_cleaners',
      'pages.services.features.standard_deep',
      'pages.services.features.standard_priority',
      'pages.services.features.standard_sessions',
    ],
    2: [
      'pages.services.features.premium_supplies',
      'pages.services.features.premium_cleaners',
      'pages.services.features.premium_deep',
      'pages.services.features.premium_priority',
      'pages.services.features.premium_sameday',
      'pages.services.features.premium_sessions',
    ],
  };

  ngOnInit(): void {
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
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

  private readonly packageIcons = ['pi pi-home', 'pi pi-star', 'pi pi-crown'];
  private readonly serviceIcons = [
    'pi pi-home', 'pi pi-briefcase', 'pi pi-car',
    'pi pi-wrench', 'pi pi-building', 'pi pi-cog',
  ];

  getPackageIcon(index: number): string {
    return this.packageIcons[index] ?? this.packageIcons[0];
  }

  getServiceIcon(index: number): string {
    return this.serviceIcons[index % this.serviceIcons.length];
  }

  /** Get the original tier index of a package (stable across sorting) */
  getPackageTierIndex(pkg: PackageListItem): number {
    const unsorted = this.packages();
    const idx = unsorted.findIndex(p => p.id === pkg.id);
    return idx >= 0 ? Math.min(idx, 2) : 0;
  }

  getPackageFeatures(tierIndex: number): string[] {
    return this.packageFeatureKeys[tierIndex] ?? this.packageFeatureKeys[0];
  }

  getIncludedServiceNames(pkg: PackageListItem): string[] {
    if (!pkg.includedServices?.length) return [];
    const lang = this.translate.currentLang || this.translate.getDefaultLang();
    return pkg.includedServices.map(svc => {
      const t = svc.translations?.[lang];
      return (t as any)?.name || svc.name || '';
    }).filter(n => !!n);
  }

  onPackageSortChange(sort: SortOption): void {
    this.packageSort.set(sort);
  }

  onServiceSortChange(sort: SortOption): void {
    this.serviceSort.set(sort);
  }

  toggleShowAllPackages(): void {
    this.showAllPackages.set(true);
  }

  toggleShowAllServices(): void {
    this.showAllServices.set(true);
  }

  bookPackage(pkg: PackageListItem): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER], {
      queryParams: { packageId: pkg.id },
    });
  }

  bookService(service: ServiceListItem): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER], {
      queryParams: { serviceId: service.id },
    });
  }

  bookNow(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER]);
  }

  private sortItems<T>(items: T[], sort: SortOption, priceField: string): T[] {
    return items.sort((a, b) => {
      switch (sort) {
        case 'price_asc':
          return ((a as any)[priceField] ?? 0) - ((b as any)[priceField] ?? 0);
        case 'price_desc':
          return ((b as any)[priceField] ?? 0) - ((a as any)[priceField] ?? 0);
        case 'name_asc':
          return this.getTranslation(a as any, 'name')
            .localeCompare(this.getTranslation(b as any, 'name'));
        default:
          return 0;
      }
    });
  }
}
