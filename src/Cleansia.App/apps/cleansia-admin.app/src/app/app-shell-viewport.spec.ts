import { TestBed } from '@angular/core/testing';
import { readFileSync } from 'fs';
import { join } from 'path';
import { AdminAuthService } from '@cleansia/admin-services';
import { MOBILE_VIEWPORT_MAX_PX } from '@cleansia/components';
import { DialogService, PageTitleService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, EMPTY } from 'rxjs';
import { AppComponent } from './app.component';

/**
 * The shell renders the mobile toolbar, and the stylesheet drops the desktop rail, from one answer
 * to "is this viewport mobile". They used to answer it separately — `< 768` here, `max-width: 768px`
 * there — and at exactly 768 px the signed-in administrator had no navigation at all.
 */
describe('admin app shell viewport mode', () => {
  function setViewportWidth(width: number): void {
    Object.defineProperty(window, 'innerWidth', { value: width, configurable: true });
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: Store, useValue: { dispatch: jest.fn() } },
        {
          provide: AdminAuthService,
          useValue: { isLoggedIn$: new BehaviorSubject(true), logout: () => EMPTY },
        },
        { provide: PageTitleService, useValue: { initialize: jest.fn() } },
        { provide: DialogService, useValue: { confirmTranslated: () => EMPTY } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    }).overrideComponent(AppComponent, { set: { template: '', imports: [] } });
  });

  it.each([
    [767, true],
    [768, true],
    [769, false],
  ])('at %i px the shell is mobile: %s', (width, mobile) => {
    setViewportWidth(width);
    const component = TestBed.createComponent(AppComponent).componentInstance;
    component.ngOnInit();

    expect(component.isMobile()).toBe(mobile);
  });

  it('follows a resize across the breakpoint', () => {
    setViewportWidth(1024);
    const component = TestBed.createComponent(AppComponent).componentInstance;
    component.ngOnInit();
    expect(component.isMobile()).toBe(false);

    setViewportWidth(768);
    window.dispatchEvent(new Event('resize'));

    expect(component.isMobile()).toBe(true);
  });

  it('collapses the rail margin at the same width the script goes mobile', () => {
    const scss = readFileSync(join(__dirname, 'app.component.scss'), 'utf-8');
    const queries = [...scss.matchAll(/@media\s*\(max-width:\s*(\d+)px\)/g)].map((m) =>
      Number(m[1])
    );

    expect(queries.length).toBeGreaterThan(0);
    expect(new Set(queries)).toEqual(new Set([MOBILE_VIEWPORT_MAX_PX]));
  });

  // The menu button is a 2.25rem glyph box in a 3.25rem toolbar; the 44px hit area is the shared
  // ring grown past that box, so the toolbar keeps its height.
  it('gives the mobile menu button the shared 44px hit area around its 2.25rem box', () => {
    const scss = readFileSync(join(__dirname, 'app.component.scss'), 'utf-8');

    expect(scss).toMatch(/@use '(\.\.\/)+libs\/shared\/assets\/src\/styles\/common\/touch-target' as \*;/);
    expect(scss).toMatch(
      /&__menu-btn\s*\{[^}]*width:\s*2\.25rem;\s*height:\s*2\.25rem;\s*position:\s*relative;\s*@include touch-target;/
    );
  });
});
