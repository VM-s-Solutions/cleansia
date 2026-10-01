import { PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CleansiaScrollTopComponent } from './cleansia-scroll-top.component';

class IntersectionObserverStub {
  static instances: IntersectionObserverStub[] = [];
  readonly observed: Element[] = [];
  readonly disconnect = jest.fn();

  constructor(private readonly callback: IntersectionObserverCallback) {
    IntersectionObserverStub.instances.push(this);
  }

  observe(target: Element): void {
    this.observed.push(target);
  }

  report(isIntersecting: boolean): void {
    this.callback([{ isIntersecting } as IntersectionObserverEntry], this as unknown as IntersectionObserver);
  }
}

describe('the scroll-to-top button', () => {
  const globals = globalThis as unknown as Record<string, unknown>;
  const originalObserver = globals['IntersectionObserver'];
  let fixture: ComponentFixture<CleansiaScrollTopComponent>;
  let hero: HTMLElement | null = null;

  function addHero(): HTMLElement {
    hero = document.createElement('section');
    hero.id = 'hero';
    document.body.appendChild(hero);
    return hero;
  }

  function render(platform: 'browser' | 'server'): HTMLButtonElement {
    TestBed.configureTestingModule({
      imports: [CleansiaScrollTopComponent],
      providers: [{ provide: PLATFORM_ID, useValue: platform }],
    });
    fixture = TestBed.createComponent(CleansiaScrollTopComponent);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).querySelector('button') as HTMLButtonElement;
  }

  function setScrollY(y: number): void {
    Object.defineProperty(window, 'scrollY', { value: y, configurable: true, writable: true });
  }

  function scrollTo(y: number): void {
    setScrollY(y);
    window.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    IntersectionObserverStub.instances = [];
    globals['IntersectionObserver'] = IntersectionObserverStub;
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    hero?.remove();
    hero = null;
    setScrollY(0);
    globals['IntersectionObserver'] = originalObserver;
  });

  it('stays hidden while the hero is on screen, however far the page scrolled, and shows once it has left', () => {
    const heroSection = addHero();
    const button = render('browser');
    const [observer] = IntersectionObserverStub.instances;
    expect(observer.observed).toEqual([heroSection]);

    observer.report(true);
    scrollTo(1000);
    expect(button.classList.contains('visible')).toBe(false);

    observer.report(false);
    fixture.detectChanges();
    expect(button.classList.contains('visible')).toBe(true);

    observer.report(true);
    fixture.detectChanges();
    expect(button.classList.contains('visible')).toBe(false);
  });

  it('keeps the 300px scroll threshold on a page without a hero', () => {
    const button = render('browser');
    expect(IntersectionObserverStub.instances).toHaveLength(0);

    scrollTo(300);
    expect(button.classList.contains('visible')).toBe(false);

    scrollTo(301);
    expect(button.classList.contains('visible')).toBe(true);
  });

  it('creates no observer during a server render', () => {
    addHero();
    render('server');

    expect(IntersectionObserverStub.instances).toHaveLength(0);
  });

  it('disconnects the hero observer on destroy', () => {
    addHero();
    render('browser');

    fixture.destroy();

    expect(IntersectionObserverStub.instances[0].disconnect).toHaveBeenCalledTimes(1);
  });
});
