import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { CleansiaSidebarMenuComponent } from './cleansia-sidebar-menu.component';

describe('CleansiaSidebarMenuComponent — brand rail', () => {
  let fixture: ComponentFixture<CleansiaSidebarMenuComponent>;
  let component: CleansiaSidebarMenuComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaSidebarMenuComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(CleansiaSidebarMenuComponent);
    component = fixture.componentInstance;
  });

  function setViewportWidth(width: number): void {
    Object.defineProperty(window, 'innerWidth', {
      value: width,
      configurable: true,
    });
    component.onResize();
  }

  it('shrinks the mark when the desktop rail collapses', () => {
    setViewportWidth(1280);
    component.toggleCollapsed();

    expect(component.effectiveCollapsed()).toBe(true);
    expect(component.brandCompact()).toBe(true);
  });

  it('keeps the full mark on mobile, where the drawer always opens full width', () => {
    setViewportWidth(1280);
    component.toggleCollapsed();
    setViewportWidth(500);

    expect(component.isMobile()).toBe(true);
    expect(component.effectiveCollapsed()).toBe(true);
    expect(component.brandCompact()).toBe(false);
  });

  it('is not compact while expanded', () => {
    setViewportWidth(1280);

    expect(component.effectiveCollapsed()).toBe(false);
    expect(component.brandCompact()).toBe(false);
  });

  it.each([
    [767, true],
    [768, true],
    [769, false],
  ])('at %i px the rail is in mobile mode: %s', (width, mobile) => {
    setViewportWidth(width);

    expect(component.isMobile()).toBe(mobile);
  });
});

/**
 * The close control is a 1.75rem glyph box in the header of a rail that clips its overflow; the
 * 44px hit area is the shared ring grown past that box, and the box sits far enough from the
 * clipped edge for the ring to fit. The stylesheet is declared an input of this project's test
 * target.
 */
describe('CleansiaSidebarMenuComponent — close control touch floor', () => {
  it('gives the mobile close control the shared 44px hit area, clear of the clipped edge', () => {
    const scss = readFileSync(
      join(
        __dirname,
        '../../../../assets/src/styles/components/cleansia-sidebar-menu.component.scss'
      ),
      'utf-8'
    );
    const close = scss.slice(scss.indexOf('.sidebar-close {'), scss.indexOf('@keyframes slideInLeft'));

    expect(close).toMatch(/width:\s*1\.75rem;\s*height:\s*1\.75rem;/);
    expect(close).toMatch(/@include touch-target;/);
    expect(close).toMatch(/right:\s*max\(0\.5rem,\s*calc\(\(\$touch-target-floor - 1\.75rem\) \/ 2\)\);/);
  });
});

/**
 * A badge is a count the reader must not lose by collapsing the rail: the one element sits beside
 * the label while expanded, and the collapsed stylesheet lifts it onto the icon's corner rather than
 * hiding it with the label and the arrow.
 */
describe('CleansiaSidebarMenuComponent — badge', () => {
  let fixture: ComponentFixture<CleansiaSidebarMenuComponent>;
  let component: CleansiaSidebarMenuComponent;

  const badges = () =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.menu-item-badge')) as HTMLElement[];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaSidebarMenuComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])],
    }).compileComponents();

    Object.defineProperty(window, 'innerWidth', { value: 1280, configurable: true });
    fixture = TestBed.createComponent(CleansiaSidebarMenuComponent);
    component = fixture.componentInstance;
    component.onResize();
    fixture.componentRef.setInput('menuItems', [
      { label: 'sidebar.notifications', icon: 'pi pi-bell', route: '/notifications', badge: '3' },
      { label: 'sidebar.employees', icon: 'pi pi-users', route: '/employee-management' },
    ]);
    fixture.detectChanges();
  });

  it('draws one badge with the count while expanded', () => {
    expect(badges()).toHaveLength(1);
    expect(badges()[0].textContent?.trim()).toBe('3');
  });

  it('keeps that badge in the rail once it collapses', () => {
    component.toggleCollapsed();
    fixture.detectChanges();

    expect(component.effectiveCollapsed()).toBe(true);
    expect(badges()).toHaveLength(1);
    expect(badges()[0].textContent?.trim()).toBe('3');
  });

  it('the collapsed stylesheet lifts the badge onto the icon corner instead of hiding it', () => {
    const scss = readFileSync(
      join(__dirname, '../../../../assets/src/styles/components/cleansia-sidebar-menu.component.scss'),
      'utf-8'
    );
    const collapsed = scss.slice(scss.indexOf('.sidebar--collapsed {'), scss.indexOf('// ===== RESPONSIVE'));
    const hidden = /\.menu-item-label,\s*\.menu-item-arrow \{\s*display: none;/;

    expect(collapsed).toMatch(hidden);
    expect(collapsed).not.toMatch(/\.menu-item-badge,/);
    expect(collapsed).toMatch(/\.menu-item-badge \{\s*position: absolute;/);
  });
});

/**
 * An entry that names a policy is drawn only for a reader who holds it; an entry that names none is
 * drawn for everyone. The gate is the sidebar's, not the shell's — the shell only declares the policy
 * on the item — so this is the one place a stubbed guard would go red.
 */
describe('CleansiaSidebarMenuComponent — permission gate', () => {
  let fixture: ComponentFixture<CleansiaSidebarMenuComponent>;
  let hasPolicy: jest.Mock;

  const labels = () =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.menu-item-label')).map((el) =>
      el.textContent?.trim()
    );

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CleansiaSidebarMenuComponent, TranslateModule.forRoot()],
      providers: [provideRouter([]), { provide: PermissionService, useValue: { hasPolicy } }],
    }).compileComponents();

    Object.defineProperty(window, 'innerWidth', { value: 1280, configurable: true });
    fixture = TestBed.createComponent(CleansiaSidebarMenuComponent);
    fixture.componentInstance.onResize();
    fixture.componentRef.setInput('menuItems', [
      { label: 'sidebar.notifications', icon: 'pi pi-bell', route: '/notifications', permission: Policy.CanViewAdminNotifications },
      { label: 'sidebar.employees', icon: 'pi pi-users', route: '/employee-management' },
    ]);
    fixture.detectChanges();
  }

  it('leaves the gated entry out for a reader without its policy, and keeps the ungated one', async () => {
    hasPolicy = jest.fn().mockReturnValue(false);

    await render();

    expect(hasPolicy).toHaveBeenCalledWith(Policy.CanViewAdminNotifications);
    expect(labels()).toEqual(['sidebar.employees']);
  });

  it('draws the gated entry for a reader who holds its policy', async () => {
    hasPolicy = jest.fn().mockImplementation((policy: string) => policy === Policy.CanViewAdminNotifications);

    await render();

    expect(labels()).toEqual(['sidebar.notifications', 'sidebar.employees']);
  });
});

/**
 * The rail is the one navigation both portals share, so its accessible names come from the app
 * bundle and the active entry says so to assistive tech — `aria-current` is what a screen reader
 * announces as "current page", the highlight class is what a sighted reader sees.
 */
describe('CleansiaSidebarMenuComponent — accessible names', () => {
  let fixture: ComponentFixture<CleansiaSidebarMenuComponent>;
  let component: CleansiaSidebarMenuComponent;

  const items = () =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.menu-item')) as HTMLElement[];

  async function render(width: number, url: string): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CleansiaSidebarMenuComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('cs', { global: { close_menu: 'Zavřít menu' }, sidebar: { orders: 'Objednávky' } });
    translate.use('cs');

    Object.defineProperty(window, 'innerWidth', { value: width, configurable: true });
    fixture = TestBed.createComponent(CleansiaSidebarMenuComponent);
    component = fixture.componentInstance;
    component.onResize();
    component.currentRoute.set(url);
    fixture.componentRef.setInput('menuItems', [
      { label: 'sidebar.orders', icon: 'pi pi-shopping-cart', route: '/orders' },
      { label: 'sidebar.invoices', icon: 'pi pi-file', route: '/invoices' },
    ]);
    fixture.detectChanges();
  }

  it('marks the entry for the current page, and only that one, as aria-current', async () => {
    await render(1280, '/orders/42');

    expect(items().map((li) => li.getAttribute('aria-current'))).toEqual(['page', null]);
    expect(items()[0].classList).toContain('menu-item--active');
  });

  it('names the mobile close control from the bundle', async () => {
    await render(500, '/orders');
    component.mobileExpanded.set(true);
    fixture.detectChanges();

    const close = (fixture.nativeElement as HTMLElement).querySelector('.sidebar-close') as HTMLElement;
    expect(close.getAttribute('aria-label')).toBe('Zavřít menu');
  });
});

