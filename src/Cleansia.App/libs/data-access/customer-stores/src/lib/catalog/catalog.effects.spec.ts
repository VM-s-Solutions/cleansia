import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  PackageListItem,
  ServiceListItem,
} from '@cleansia/customer-services';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { Subject, of, throwError } from 'rxjs';
import * as CatalogActions from './catalog.actions';
import { CustomerCatalogEffects } from './catalog.effects';

const SERVICES = [ServiceListItem.fromJS({ id: 'svc-1', name: 'Standard' })];
const PACKAGES = [PackageListItem.fromJS({ id: 'pkg-1', name: 'Deep clean' })];

describe('CustomerCatalogEffects', () => {
  let actions$: Subject<Action>;
  let serviceClient: { getOverview: jest.Mock };
  let packageClient: { getOverview: jest.Mock };

  const createEffects = (): CustomerCatalogEffects => {
    TestBed.configureTestingModule({
      providers: [
        CustomerCatalogEffects,
        provideMockActions(() => actions$),
        { provide: CustomerClient, useValue: { serviceClient, packageClient } },
      ],
    });
    return TestBed.inject(CustomerCatalogEffects);
  };

  /** Subscribe first: `actions$` is a Subject, so anything pushed before this is lost. */
  const collect = (source: { subscribe: (fn: (a: Action) => void) => void }) => {
    const emitted: Action[] = [];
    source.subscribe((action) => emitted.push(action));
    return emitted;
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    actions$ = new Subject<Action>();
    serviceClient = { getOverview: jest.fn() };
    packageClient = { getOverview: jest.fn() };
  });

  describe('loadServices$', () => {
    it('emits the services the catalog returned', () => {
      serviceClient.getOverview.mockReturnValue(of(SERVICES));

      const emitted = collect(createEffects().loadServices$);
      actions$.next(CatalogActions.loadCustomerServices());

      expect(emitted).toEqual([
        CatalogActions.loadCustomerServicesSuccess({ services: SERVICES }),
      ]);
    });

    it('reads the catalog through the customer service client, never the package one', () => {
      serviceClient.getOverview.mockReturnValue(of(SERVICES));

      collect(createEffects().loadServices$);
      actions$.next(CatalogActions.loadCustomerServices());

      expect(serviceClient.getOverview).toHaveBeenCalledTimes(1);
      expect(packageClient.getOverview).not.toHaveBeenCalled();
    });

    it('maps a failure to loadCustomerServicesFailure carrying the error', () => {
      const failure = { message: 'offline' };
      serviceClient.getOverview.mockReturnValue(throwError(() => failure));

      const emitted = collect(createEffects().loadServices$);
      actions$.next(CatalogActions.loadCustomerServices());

      expect(emitted).toHaveLength(1);
      expect(emitted[0].type).toBe(CatalogActions.loadCustomerServicesFailure.type);
      expect(emitted[0]).toMatchObject({ error: failure });
    });

    // The `catchError` lives INSIDE the switchMap. Hoisting it to the outer pipe still compiles and
    // still reports the first failure — but the effect stream then completes and the catalog can
    // never be reloaded for the rest of the session.
    it('stays alive after a failure, so the retry is still served', () => {
      serviceClient.getOverview
        .mockReturnValueOnce(throwError(() => ({ message: 'offline' })))
        .mockReturnValueOnce(of(SERVICES));

      const emitted = collect(createEffects().loadServices$);
      actions$.next(CatalogActions.loadCustomerServices());
      actions$.next(CatalogActions.loadCustomerServices());

      expect(emitted.map((a) => a.type)).toEqual([
        CatalogActions.loadCustomerServicesFailure.type,
        CatalogActions.loadCustomerServicesSuccess.type,
      ]);
    });

    // `switchMap`, not `mergeMap`: a slow first response must not land after a newer one and
    // repaint the catalog with stale prices.
    it('abandons an in-flight read when a newer load starts', () => {
      const first = new Subject<ServiceListItem[]>();
      const second = new Subject<ServiceListItem[]>();
      serviceClient.getOverview
        .mockReturnValueOnce(first)
        .mockReturnValueOnce(second);

      const emitted = collect(createEffects().loadServices$);
      actions$.next(CatalogActions.loadCustomerServices());
      actions$.next(CatalogActions.loadCustomerServices());

      second.next(SERVICES);
      first.next([]);

      expect(emitted).toEqual([
        CatalogActions.loadCustomerServicesSuccess({ services: SERVICES }),
      ]);
    });
  });

  describe('loadPackages$', () => {
    it('emits the packages the catalog returned', () => {
      packageClient.getOverview.mockReturnValue(of(PACKAGES));

      const emitted = collect(createEffects().loadPackages$);
      actions$.next(CatalogActions.loadCustomerPackages());

      expect(emitted).toEqual([
        CatalogActions.loadCustomerPackagesSuccess({ packages: PACKAGES }),
      ]);
    });

    it('maps a failure to loadCustomerPackagesFailure and stays alive for the retry', () => {
      packageClient.getOverview
        .mockReturnValueOnce(throwError(() => ({ message: 'offline' })))
        .mockReturnValueOnce(of(PACKAGES));

      const emitted = collect(createEffects().loadPackages$);
      actions$.next(CatalogActions.loadCustomerPackages());
      actions$.next(CatalogActions.loadCustomerPackages());

      expect(emitted.map((a) => a.type)).toEqual([
        CatalogActions.loadCustomerPackagesFailure.type,
        CatalogActions.loadCustomerPackagesSuccess.type,
      ]);
    });

    it('does not answer the services action', () => {
      const emitted = collect(createEffects().loadPackages$);
      actions$.next(CatalogActions.loadCustomerServices());

      expect(emitted).toEqual([]);
      expect(packageClient.getOverview).not.toHaveBeenCalled();
    });
  });
});
