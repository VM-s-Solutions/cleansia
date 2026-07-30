import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
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
});
