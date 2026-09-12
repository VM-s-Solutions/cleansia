import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { PackageListItem, ServiceListItem } from '@cleansia/customer-services';
import {
  loadCustomerCurrencies,
  loadCustomerPackages,
  loadCustomerServices,
  selectCustomerCatalogLoading,
  selectCustomerDefaultCurrencyCode,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { ServicesCatalogComponent } from './services-catalog.component';

function pkg(
  id: string,
  name: string,
  price: number,
  extra: { czName?: string; isPopular?: boolean; includes?: string[] } = {}
): PackageListItem {
  return PackageListItem.fromJS({
    id,
    name,
    price,
    isPopular: extra.isPopular ?? false,
    translations: extra.czName ? { cs: { name: extra.czName } } : undefined,
    includedServices: extra.includes?.map((n, i) => ({ id: `${id}-s${i}`, name: n })),
  });
}

function svc(id: string, name: string, basePrice: number, perRoomPrice = 0): ServiceListItem {
  return ServiceListItem.fromJS({ id, name, basePrice, perRoomPrice });
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

  function build(
    packages: PackageListItem[] = [],
    services: ServiceListItem[] = [],
    defaultCurrencyCode: string | null = 'CZK',
  ): void {
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
            { selector: selectCustomerDefaultCurrencyCode, value: defaultCurrencyCode },
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

  const names = (items: { name?: string }[]): (string | undefined)[] => items.map(i => i.name);

  afterEach(() => TestBed.resetTestingModule());

  it('asks the store for both catalogs and the currency they are priced in on init', () => {
    build();

    component.ngOnInit();

    expect(store.dispatch).toHaveBeenCalledWith(loadCustomerServices());
    expect(store.dispatch).toHaveBeenCalledWith(loadCustomerPackages());
    expect(store.dispatch).toHaveBeenCalledWith(loadCustomerCurrencies());
  });

  describe('sorting the services', () => {
    it('orders them cheapest first by default', () => {
      build([], [svc('s1', 'Windows', 400), svc('s2', 'Ironing', 200)]);

      expect(names(component.sortedServices())).toEqual(['Ironing', 'Windows']);
    });

    it('reverses to dearest first', () => {
      build([], [svc('s1', 'Windows', 400), svc('s2', 'Ironing', 200)]);

      component.onServiceSortChange('price_desc');

      expect(names(component.sortedServices())).toEqual(['Windows', 'Ironing']);
    });

    it('orders by the translated name, not the raw one', () => {
      build(
        [],
        [
          ServiceListItem.fromJS({ id: 's1', name: 'Alpha', basePrice: 1, perRoomPrice: 0, translations: { cs: { name: 'Zebra' } } }),
          ServiceListItem.fromJS({ id: 's2', name: 'Zulu', basePrice: 2, perRoomPrice: 0, translations: { cs: { name: 'Andulka' } } }),
        ]
      );
      currentLang = 'cs';

      component.onServiceSortChange('name_asc');

      expect(names(component.sortedServices())).toEqual(['Zulu', 'Alpha']);
    });

    it('never reorders the store collection itself', () => {
      build([], [svc('s1', 'Windows', 400), svc('s2', 'Ironing', 200)]);

      component.onServiceSortChange('price_desc');
      component.sortedServices();

      expect(names(component.services())).toEqual(['Windows', 'Ironing']);
    });
  });

  describe('the package preview', () => {
    it('leads with the three cheapest', () => {
      build(FIVE_PACKAGES);

      expect(names(component.visiblePackages()).sort()).toEqual(['Basic', 'Deluxe', 'Standard']);
      expect(component.hasMorePackages()).toBe(true);
    });

    it('puts the recommended package in the middle, where the inverted card belongs', () => {
      // Cheapest-first alone would leave it third, where an inverted card reads
      // as the dearest option rather than the suggested one.
      build([
        pkg('p1', 'Deluxe', 100),
        pkg('p2', 'Standard', 300),
        pkg('p3', 'Basic', 500, { isPopular: true }),
        pkg('p4', 'Premium', 900),
      ]);

      expect(names(component.visiblePackages())).toEqual(['Deluxe', 'Basic', 'Standard']);
    });

    it('leaves the order alone when the recommended one is already in the middle', () => {
      build([
        pkg('p1', 'Deluxe', 100),
        pkg('p2', 'Standard', 300, { isPopular: true }),
        pkg('p3', 'Basic', 500),
        pkg('p4', 'Premium', 900),
      ]);

      expect(names(component.visiblePackages())).toEqual(['Deluxe', 'Standard', 'Basic']);
    });

    it('leaves the order alone when the recommended one is not in the preview at all', () => {
      build([
        pkg('p1', 'Deluxe', 100),
        pkg('p2', 'Standard', 300),
        pkg('p3', 'Basic', 500),
        pkg('p4', 'Premium', 900, { isPopular: true }),
      ]);

      expect(names(component.visiblePackages())).toEqual(['Deluxe', 'Standard', 'Basic']);
    });

    it('shows the whole catalog once the link is used, and drops the link', () => {
      build(FIVE_PACKAGES);

      component.revealAllPackages();

      expect(component.visiblePackages()).toHaveLength(5);
      expect(component.hasMorePackages()).toBe(false);
    });

    it('offers no link when the catalog already fits', () => {
      build(FIVE_PACKAGES.slice(0, 3));

      expect(component.visiblePackages()).toHaveLength(3);
      expect(component.hasMorePackages()).toBe(false);
    });
  });

  describe('what a service card says its price buys', () => {
    it('marks a service that charges by the room', () => {
      build();

      expect(component.isPricedPerRoom(svc('s1', 'General', 500, 150))).toBe(true);
    });

    it('treats a flat service as a single price', () => {
      build();

      expect(component.isPricedPerRoom(svc('s1', 'Bathroom', 300))).toBe(false);
    });
  });

  describe('what a package card lists', () => {
    it('names the services the package actually includes', () => {
      build([pkg('p1', 'Basic', 500, { includes: ['Windows', 'Bathroom'] })]);

      expect(component.getIncludedServiceNames(component.packages()[0])).toEqual([
        'Windows',
        'Bathroom',
      ]);
    });

    it('lists nothing rather than inventing a feature list', () => {
      build([pkg('p1', 'Basic', 500)]);

      expect(component.getIncludedServiceNames(component.packages()[0])).toEqual([]);
    });
  });

  describe('mascots', () => {
    it('cycles the service poses so a long catalog never renders a blank card', () => {
      build();

      expect(component.serviceMascot(8)).toBe(component.serviceMascot(0));
      expect(component.serviceMascot(9)).toBe(component.serviceMascot(1));
    });

    it('cycles the package poses too', () => {
      build();

      expect(component.packageMascot(3)).toBe(component.packageMascot(0));
    });

    it('only ever uses the normalised tiles, so every character renders the same size', () => {
      build();

      const used = [0, 1, 2, 3, 4, 5, 6, 7]
        .map(i => component.serviceMascot(i))
        .concat([0, 1, 2].map(i => component.packageMascot(i)));

      expect(used.every(src => src.endsWith('-tile.webp'))).toBe(true);
      expect(new Set(used).size).toBe(used.length);
    });
  });

  describe('translation fallback', () => {
    it('prefers the current language', () => {
      build([pkg('p1', 'Basic', 100, { czName: 'Základní' })]);
      currentLang = 'cs';

      expect(component.getTranslation(component.packages()[0], 'name')).toBe('Základní');
    });

    it('falls back to the base field when the language has no entry', () => {
      build([pkg('p1', 'Basic', 100, { czName: 'Základní' })]);
      currentLang = 'uk';

      expect(component.getTranslation(component.packages()[0], 'name')).toBe('Basic');
    });

    it('returns an empty string rather than undefined when nothing is set', () => {
      build([pkg('p1', 'Basic', 100)]);

      expect(component.getTranslation(component.packages()[0], 'description')).toBe('');
    });
  });

  describe('booking', () => {
    it('carries the chosen package into the wizard', () => {
      build(FIVE_PACKAGES);

      component.bookPackage(FIVE_PACKAGES[0]);

      expect(router.navigate).toHaveBeenCalledWith(['order'], { queryParams: { packageId: 'p1' } });
    });

    it('carries the chosen service into the wizard', () => {
      build();

      component.bookService(svc('s1', 'Windows', 400));

      expect(router.navigate).toHaveBeenCalledWith(['order'], { queryParams: { serviceId: 's1' } });
    });

    it('opens the wizard empty when nothing is preselected', () => {
      build();

      component.bookNow();

      expect(router.navigate).toHaveBeenCalledWith(['order']);
    });

    it('sends the calculator link to the hero that holds it', () => {
      build();

      component.openCalculator();

      expect(router.navigate).toHaveBeenCalledWith(['/'], { fragment: 'hero' });
    });
  });

  describe('price formatting', () => {
    it('drops the trailing zeroes a whole crown price would otherwise carry', () => {
      build();

      expect(component.formatPrice(1200)).not.toMatch(/[.,]\d/);
      expect(component.formatPrice(1200).replace(/\D/g, '')).toBe('1200');
    });

    it('renders in the platform default currency, which is what the catalogue is priced in', () => {
      build([], [], 'CZK');

      expect(component.formatPrice(1200)).toContain('Kč');
    });

    it('follows the default currency rather than assuming crowns', () => {
      build([], [], 'EUR');

      expect(component.formatPrice(1200)).toContain('€');
      expect(component.formatPrice(1200)).not.toContain('Kč');
    });

    it('prints a bare figure until the default currency is known', () => {
      build([], [], null);

      expect(component.formatPrice(1200).replace(/\D/g, '')).toBe('1200');
      expect(component.formatPrice(1200)).not.toContain('Kč');
    });
  });
});
