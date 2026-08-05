import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { PackageListItem, ServiceListItem } from '@cleansia/customer-services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  selectCustomerCatalogLoading,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { ServicesCatalogComponent } from './services-catalog.component';

function pkg(id: string, name: string, price: number, czName?: string): PackageListItem {
  return PackageListItem.fromJS({
    id,
    name,
    price,
    translations: czName ? { cs: { name: czName } } : undefined,
  });
}

function svc(id: string, name: string, basePrice: number): ServiceListItem {
  return ServiceListItem.fromJS({ id, name, basePrice, perRoomPrice: 0 });
}

describe('ServicesCatalogComponent', () => {
  let component: ServicesCatalogComponent;
  let store: MockStore;
  let router: { navigate: jest.Mock };
  let currentLang: string;

  const FIVE_PACKAGES = [
    pkg('p1', 'Basic', 500),
    pkg('p2', 'Standard', 300),
    pkg('p3', 'Premium', 900),
    pkg('p4', 'Deluxe', 100),
    pkg('p5', 'Ultimate', 700),
  ];

  function build(packages: PackageListItem[] = [], services: ServiceListItem[] = []): void {
    currentLang = 'en';
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ServicesCatalogComponent,
        provideMockStore({
          selectors: [
            { selector: selectCustomerPackages, value: packages },
            { selector: selectCustomerServices, value: services },
            { selector: selectCustomerCatalogLoading, value: false },
          ],
        }),
        { provide: Router, useValue: router },
        {
          provide: TranslateService,
          useValue: {
            get currentLang() {
              return currentLang;
            },
            getDefaultLang: () => 'en',
          },
        },
      ],
    });

    store = TestBed.inject(MockStore);
    jest.spyOn(store, 'dispatch');
    component = TestBed.inject(ServicesCatalogComponent);
  }

  const names = (items: { name?: string }[]): (string | undefined)[] => items.map((i) => i.name);

  afterEach(() => TestBed.resetTestingModule());

  it('asks the store for both catalogs on init', () => {
    build();

    component.ngOnInit();

    expect(store.dispatch).toHaveBeenCalledWith(loadCustomerServices());
    expect(store.dispatch).toHaveBeenCalledWith(loadCustomerPackages());
  });

  describe('sorting', () => {
    it('orders packages cheapest first by default', () => {
      build(FIVE_PACKAGES);

      expect(names(component.sortedPackages())).toEqual([
        'Deluxe',
        'Standard',
        'Basic',
        'Ultimate',
        'Premium',
      ]);
    });

    it('reverses to dearest first', () => {
      build(FIVE_PACKAGES);

      component.onPackageSortChange('price_desc');

      expect(names(component.sortedPackages())).toEqual([
        'Premium',
        'Ultimate',
        'Basic',
        'Standard',
        'Deluxe',
      ]);
    });

    it('orders by the translated name, not the raw one', () => {
      build([pkg('p1', 'Alpha', 100, 'Zebra'), pkg('p2', 'Zulu', 200, 'Andulka')]);
      currentLang = 'cs';

      component.onPackageSortChange('name_asc');

      expect(names(component.sortedPackages())).toEqual(['Zulu', 'Alpha']);
    });

    it('sorts services on their own base price, independently of the package sort', () => {
      build(FIVE_PACKAGES, [svc('s1', 'Windows', 400), svc('s2', 'Ironing', 200)]);

      component.onServiceSortChange('price_desc');

      expect(names(component.sortedServices())).toEqual(['Windows', 'Ironing']);
      expect(names(component.sortedPackages())[0]).toBe('Deluxe');
    });

    it('never reorders the store collection itself', () => {
      build(FIVE_PACKAGES);

      component.onPackageSortChange('price_desc');
      component.sortedPackages();

      expect(names(component.packages())).toEqual([
        'Basic',
        'Standard',
        'Premium',
        'Deluxe',
        'Ultimate',
      ]);
    });
  });

  describe('progressive disclosure', () => {
    it('shows the first three and teases the next three', () => {
      build(FIVE_PACKAGES);

      expect(names(component.visiblePackages())).toEqual(['Deluxe', 'Standard', 'Basic']);
      expect(names(component.teaserPackages())).toEqual(['Ultimate', 'Premium']);
      expect(component.hasMorePackages()).toBe(true);
    });

    it('drops the teaser and the button once everything is shown', () => {
      build(FIVE_PACKAGES);

      component.toggleShowAllPackages();

      expect(component.visiblePackages()).toHaveLength(5);
      expect(component.teaserPackages()).toEqual([]);
      expect(component.hasMorePackages()).toBe(false);
    });

    it('offers no expansion when the catalog already fits', () => {
      build(FIVE_PACKAGES.slice(0, 3));

      expect(component.visiblePackages()).toHaveLength(3);
      expect(component.teaserPackages()).toEqual([]);
      expect(component.hasMorePackages()).toBe(false);
    });
  });

  describe('tier features', () => {
    it('keys the tier off the catalog order, not the sorted position', () => {
      build(FIVE_PACKAGES);
      component.onPackageSortChange('price_desc');

      const dearest = component.sortedPackages()[0];

      expect(dearest.name).toBe('Premium');
      expect(component.getPackageTierIndex(dearest)).toBe(2);
      expect(component.getPackageTierIndex(component.packages()[0])).toBe(0);
    });

    it('clamps every package past the third onto the top tier', () => {
      build(FIVE_PACKAGES);

      expect(component.getPackageTierIndex(FIVE_PACKAGES[4])).toBe(2);
    });

    it('falls back to the entry tier for a package the store does not hold', () => {
      build(FIVE_PACKAGES);

      expect(component.getPackageTierIndex(pkg('ghost', 'Ghost', 1))).toBe(0);
      expect(component.getPackageFeatures(99)).toEqual(component.getPackageFeatures(0));
    });
  });

  describe('translation fallback', () => {
    it('prefers the current language', () => {
      build([pkg('p1', 'Basic', 100, 'Základní')]);
      currentLang = 'cs';

      expect(component.getTranslation(component.packages()[0], 'name')).toBe('Základní');
    });

    it('falls back to the base field when the language has no entry', () => {
      build([pkg('p1', 'Basic', 100, 'Základní')]);
      currentLang = 'uk';

      expect(component.getTranslation(component.packages()[0], 'name')).toBe('Basic');
    });

    it('returns an empty string rather than undefined when nothing is set', () => {
      build([pkg('p1', 'Basic', 100)]);

      expect(component.getTranslation(component.packages()[0], 'description')).toBe('');
    });
  });

  describe('icons', () => {
    it('gives each of the three tiers its own icon and clamps beyond them', () => {
      build();

      expect(component.getPackageIcon(0)).toBe('pi pi-home');
      expect(component.getPackageIcon(2)).toBe('pi pi-crown');
      expect(component.getPackageIcon(7)).toBe('pi pi-home');
    });

    it('cycles service icons so a long catalog never renders a blank one', () => {
      build();

      expect(component.getServiceIcon(6)).toBe(component.getServiceIcon(0));
      expect(component.getServiceIcon(13)).toBe(component.getServiceIcon(1));
    });
  });

  describe('booking', () => {
    it('carries the chosen package into the wizard', () => {
      build(FIVE_PACKAGES);

      component.bookPackage(FIVE_PACKAGES[0]);

      expect(router.navigate).toHaveBeenCalledWith(['order'], {
        queryParams: { packageId: 'p1' },
      });
    });

    it('carries the chosen service into the wizard', () => {
      build();

      component.bookService(svc('s1', 'Windows', 400));

      expect(router.navigate).toHaveBeenCalledWith(['order'], {
        queryParams: { serviceId: 's1' },
      });
    });

    it('opens the wizard empty when nothing is preselected', () => {
      build();

      component.bookNow();

      expect(router.navigate).toHaveBeenCalledWith(['order']);
    });
  });

  describe('price formatting', () => {
    it('drops the trailing zeroes a whole crown price would otherwise carry', () => {
      build();

      expect(component.formatPrice(1200)).not.toMatch(/[.,]\d/);
      expect(component.formatPrice(1200).replace(/\D/g, '')).toBe('1200');
    });

    it('renders in crowns', () => {
      build();

      expect(component.formatPrice(1200)).toContain('Kč');
    });
  });
});
