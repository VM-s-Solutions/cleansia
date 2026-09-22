import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { CleansiaMobileToolbarComponent } from './cleansia-mobile-toolbar.component';

@Component({
  standalone: true,
  imports: [CleansiaMobileToolbarComponent],
  template: `
    <cleansia-mobile-toolbar (menuOpen)="opened = opened + 1">
      <span class="cleansia-mobile-toolbar__action" id="bell">bell</span>
    </cleansia-mobile-toolbar>
  `,
})
class HostComponent {
  opened = 0;
}

describe('CleansiaMobileToolbarComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('cs', {
      global: { open_menu: 'Otevřít menu' },
      components: { brand_mark_alt: 'Cleansia' },
    });
    translate.use('cs');

    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  const root = (): HTMLElement => fixture.nativeElement.querySelector('.cleansia-mobile-toolbar');
  const menuButton = (): HTMLButtonElement =>
    root().querySelector('.cleansia-mobile-toolbar__menu button') as HTMLButtonElement;

  it('names the menu button from the bundle, so the label follows the session language', () => {
    expect(menuButton().getAttribute('aria-label')).toBe('Otevřít menu');
  });

  it('asks the shell to open the drawer when the menu button is pressed', () => {
    menuButton().click();

    expect(fixture.componentInstance.opened).toBe(1);
  });

  it('draws the mark through the brand component, alone', () => {
    const brand = root().querySelector('.cleansia-mobile-toolbar__brand') as HTMLElement;

    expect(brand.querySelector('cleansia-brand-name')).not.toBeNull();
    expect(brand.textContent?.trim()).toBe('');
  });

  it('projects the app’s own controls in front of the language switcher', () => {
    const actions = root().querySelector('.cleansia-mobile-toolbar__actions') as HTMLElement;
    const children = Array.from(actions.children);

    expect(children.findIndex((el) => el.id === 'bell')).toBe(0);
    expect(children[children.length - 1].tagName.toLowerCase()).toBe('cleansia-language-switcher');
  });
});

/**
 * Every control in the bar is the same 2.25rem box in a 3.25rem bar. The menu button and a
 * projected action are `cleansia-button`s, whose 44px hit area is the ring grown past the painted
 * box in both renderings — the inner <button>, and the anchor a link action renders as (pinned in
 * the button's own spec) — so the stylesheet must leave that ring reachable and size the box, not
 * the hit area. The stylesheet is declared an input of this project's test target.
 */
describe('CleansiaMobileToolbarComponent — stylesheet', () => {
  const scss = readFileSync(
    join(__dirname, '../../../../assets/src/styles/components/cleansia-mobile-toolbar.component.scss'),
    'utf-8'
  );

  it('sizes the bar and its controls once', () => {
    expect(scss).toMatch(/\.cleansia-mobile-toolbar\s*\{[\s\S]*height:\s*3\.25rem;/);
    expect(scss).toMatch(/&__menu,\s*&__action\s*\{[\s\S]*width:\s*2\.25rem;\s*height:\s*2\.25rem;/);
    expect(scss).not.toMatch(/overflow:\s*hidden/);
  });

  it('sits on the fixed-page-element band of the documented z-index scale', () => {
    expect(scss).toMatch(/\.cleansia-mobile-toolbar\s*\{[\s\S]*z-index:\s*1[0-9]{2};/);
  });

  // A printed invoice or order is the page, not the shell: the rail, its backdrop and the toolbar
  // leave the sheet and the content takes the rail's margin back. Both portals print.
  it('hides the rail and the toolbar and releases the content margin on paper', () => {
    const print = scss.slice(scss.indexOf('@media print'));

    expect(print).toMatch(/cleansia-sidebar-menu,[\s\S]*\.cleansia-mobile-toolbar\s*\{[\s\S]*display:\s*none !important;/);
    expect(print).toMatch(/\.main-content\s*\{[\s\S]*&__margin\s*\{\s*padding-left:\s*0 !important;/);
  });
});
