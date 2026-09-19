import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
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
