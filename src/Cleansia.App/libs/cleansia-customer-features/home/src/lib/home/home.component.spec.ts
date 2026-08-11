import { ElementRef, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { loadCustomerPackages, loadCustomerServices } from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { HomeComponent } from './home.component';

class ObserverStub {
  static instances: ObserverStub[] = [];
  readonly observed: unknown[] = [];
  disconnect = jest.fn();

  constructor(..._args: unknown[]) {
    ObserverStub.instances.push(this);
  }

  observe(target: unknown): void {
    this.observed.push(target);
  }
}

function elementAt(top: number): HTMLElement {
  const el = document.createElement('div');
  el.className = 'animate-on-scroll';
  el.getBoundingClientRect = () => ({ top }) as DOMRect;
  return el;
}

describe('HomeComponent', () => {
  let host: HTMLElement;
  let store: MockStore;

  const VIEWPORT_HEIGHT = 800;

  function build(platform: 'server' | 'browser'): HomeComponent {
    TestBed.configureTestingModule({
      providers: [
        HomeComponent,
        provideMockStore(),
        { provide: PLATFORM_ID, useValue: platform },
        { provide: ElementRef, useValue: new ElementRef(host) },
      ],
    });
    store = TestBed.inject(MockStore);
    jest.spyOn(store, 'dispatch');
    return TestBed.inject(HomeComponent);
  }

  beforeEach(() => {
    host = document.createElement('div');
    ObserverStub.instances = [];
    window.innerHeight = VIEWPORT_HEIGHT;
    (global as unknown as Record<string, unknown>)['IntersectionObserver'] = ObserverStub;
    (global as unknown as Record<string, unknown>)['MutationObserver'] = ObserverStub;
  });

  afterEach(() => TestBed.resetTestingModule());

  it('asks the store for the catalog the landing page renders', () => {
    const component = build('browser');

    component.ngOnInit();

    expect(store.dispatch).toHaveBeenCalledWith(loadCustomerServices());
    expect(store.dispatch).toHaveBeenCalledWith(loadCustomerPackages());
  });

  it('installs no observer during a server render', () => {
    const component = build('server');
    host.appendChild(elementAt(VIEWPORT_HEIGHT + 100));

    component.ngAfterViewInit();

    expect(ObserverStub.instances).toHaveLength(0);
  });

  it('installs the scroll observers in the browser', () => {
    const component = build('browser');

    component.ngAfterViewInit();

    expect(ObserverStub.instances).toHaveLength(2);
  });

  it('leaves above-the-fold content visible so the server-rendered paint is never blanked', () => {
    const component = build('browser');
    const aboveTheFold = elementAt(VIEWPORT_HEIGHT - 1);
    host.appendChild(aboveTheFold);

    component.ngAfterViewInit();

    expect(aboveTheFold.classList.contains('section-visible')).toBe(true);
    expect(aboveTheFold.classList.contains('anim-pending')).toBe(false);
    expect(ObserverStub.instances[0].observed).toHaveLength(0);
  });

  it('hides and observes only what starts below the fold', () => {
    const component = build('browser');
    const belowTheFold = elementAt(VIEWPORT_HEIGHT + 1);
    host.appendChild(belowTheFold);

    component.ngAfterViewInit();

    expect(belowTheFold.classList.contains('anim-pending')).toBe(true);
    expect(belowTheFold.classList.contains('section-visible')).toBe(false);
    expect(ObserverStub.instances[0].observed).toEqual([belowTheFold]);
  });

  it('never re-observes an element it has already handled', () => {
    const component = build('browser');
    host.appendChild(elementAt(VIEWPORT_HEIGHT + 1));

    component.ngAfterViewInit();
    host.appendChild(elementAt(VIEWPORT_HEIGHT + 2));
    component.ngAfterViewInit();

    expect(ObserverStub.instances[0].observed).toHaveLength(1);
    expect(ObserverStub.instances[2].observed).toHaveLength(1);
  });

  it('disconnects both observers on destroy', () => {
    const component = build('browser');

    component.ngAfterViewInit();
    component.ngOnDestroy();

    for (const observer of ObserverStub.instances) {
      expect(observer.disconnect).toHaveBeenCalledTimes(1);
    }
  });

  it('destroys cleanly after a server render that installed nothing', () => {
    const component = build('server');

    component.ngAfterViewInit();

    expect(() => component.ngOnDestroy()).not.toThrow();
  });
});
