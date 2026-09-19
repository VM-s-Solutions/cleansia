import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { readdirSync, readFileSync } from 'fs';
import { join, relative } from 'path';
import { CleansiaButtonComponent } from './cleansia-button.component';

const STYLES_DIR = join(__dirname, '../../../../assets/src/styles');
const WORKSPACE_ROOT = join(__dirname, '../../../../../..');

describe('CleansiaButtonComponent (a11y)', () => {
  let fixture: ComponentFixture<CleansiaButtonComponent>;
  let component: CleansiaButtonComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaButtonComponent, TranslateModule.forRoot()],
    }).compileComponents();

    fixture = TestBed.createComponent(CleansiaButtonComponent);
    component = fixture.componentInstance;
  });

  function nativeButton(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button');
  }

  it('exposes the ariaLabel on the rendered button for an icon-only button', () => {
    fixture.componentRef.setInput('icon', 'pi pi-trash');
    fixture.componentRef.setInput('ariaLabel', 'Delete item');
    fixture.detectChanges();

    expect(component.isIconOnly()).toBe(true);
    expect(nativeButton().getAttribute('aria-label')).toBe('Delete item');
  });

  it('keeps the icon-only button reachable as a native button (has accessible name, not just an icon)', () => {
    fixture.componentRef.setInput('icon', 'pi pi-plus');
    fixture.componentRef.setInput('ariaLabel', 'Add row');
    fixture.detectChanges();

    const btn = nativeButton();
    expect(btn).toBeTruthy();
    expect(btn.getAttribute('aria-label')).toBe('Add row');
  });

  it('does not emit an aria-label when a visible text label is present', () => {
    fixture.componentRef.setInput('label', 'Save');
    fixture.componentRef.setInput('ariaLabel', 'Save the form');
    fixture.detectChanges();

    expect(component.isIconOnly()).toBe(false);
    expect(nativeButton().getAttribute('aria-label')).toBeNull();
  });

  it('emits no aria-label attribute when ariaLabel is not provided', () => {
    fixture.componentRef.setInput('icon', 'pi pi-cog');
    fixture.detectChanges();

    expect(nativeButton().getAttribute('aria-label')).toBeNull();
  });
});

/**
 * A labelled action in a fixed-width slot shrinks to 5rem on a phone and shows an initial and an
 * ellipsis. `auto-width` is the content-sized slot; it needs both halves — the class on the host and
 * the stylesheet lifting the base min-width — or the button is still a full-row slab.
 */
describe('CleansiaButtonComponent (content-sized action)', () => {
  it('puts the auto-width class on the host', async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaButtonComponent, TranslateModule.forRoot()],
    }).compileComponents();
    const fixture = TestBed.createComponent(CleansiaButtonComponent);
    fixture.componentRef.setInput('label', 'Export my data');
    fixture.componentRef.setInput('size', 'auto-width');
    fixture.detectChanges();

    const classes = fixture.componentInstance.cssClasses().split(' ');
    expect(classes).toContain('auto-width');
    expect(classes.some((c) => /-width$/.test(c) && c !== 'auto-width')).toBe(false);
  });

  it('lifts the full-row minimum in the stylesheet for that size', () => {
    const scss = readFileSync(
      join(STYLES_DIR, 'components/cleansia-button.component.scss'),
      'utf-8'
    );

    expect(scss).toMatch(/\.cleansia-button\.auto-width\s*\{[^}]*min-width:\s*0;/);
  });

  /**
   * The fixed slots shrink to 5rem on a phone, where a labelled action in one is an initial and an
   * ellipsis; every template that puts a label in one is the same defect again. An icon-only button
   * in a small slot is fine — it has nothing to truncate.
   */
  it('keeps every labelled button out of the small fixed slots, in every template', () => {
    const offenders: string[] = [];
    for (const dir of ['libs', 'apps']) {
      const root = join(WORKSPACE_ROOT, dir);
      for (const entry of readdirSync(root, { recursive: true, withFileTypes: true })) {
        if (!entry.isFile() || !entry.name.endsWith('.component.html')) {
          continue;
        }
        const file = join(entry.parentPath, entry.name);
        const html = readFileSync(file, 'utf-8');
        for (const tag of openingTags(html, 'cleansia-button')) {
          const labelled = /(^|\s)\[?(label|title)\]?\s*=/.test(tag.attributes);
          const smallSlot = /(^|\s)\[?size\]?\s*=\s*"'?(xx-small|x-small|small)-width'?"/.test(
            tag.attributes
          );
          if (labelled && smallSlot) {
            offenders.push(`${relative(WORKSPACE_ROOT, file)}:${tag.line}`);
          }
        }
      }
    }

    expect(offenders).toEqual([]);
  });
});

function openingTags(html: string, element: string): { attributes: string; line: number }[] {
  const tags: { attributes: string; line: number }[] = [];
  const open = `<${element}`;
  let from = html.indexOf(open);
  while (from !== -1) {
    let end = from + open.length;
    let quote: string | null = null;
    while (end < html.length && (quote !== null || html[end] !== '>')) {
      const char = html[end];
      if (quote === null && (char === '"' || char === "'")) {
        quote = char;
      } else if (char === quote) {
        quote = null;
      }
      end++;
    }
    tags.push({
      attributes: html.slice(from + open.length, end).replace(/\/$/, ''),
      line: html.slice(0, from).split('\n').length,
    });
    from = html.indexOf(open, end);
  }
  return tags;
}

/**
 * A control a thumb has to hit needs a 44px box, and the painted pill keeps its ruled height: the
 * floor is a pseudo-element grown past the box, which the pointer can only reach because the button
 * no longer clips its overflow — so the ripple, the reason for that clip, rounds itself instead.
 * jsdom lays nothing out, so the pins read the stylesheets, both declared inputs of this project's
 * test target.
 */
describe('CleansiaButtonComponent (touch floor)', () => {
  const button = readFileSync(
    join(STYLES_DIR, 'components/cleansia-button.component.scss'),
    'utf-8'
  );

  it('states the floor once, as a hit area grown past the box only where the box is short', () => {
    const mixin = readFileSync(join(STYLES_DIR, 'common/touch-target.scss'), 'utf-8');

    expect(mixin).toMatch(/\$touch-target-floor:\s*44px;/);
    expect(mixin).toMatch(
      /@mixin touch-target[\s\S]*&::after\s*\{[^}]*inset:\s*min\(0px,\s*calc\(\(100% - #\{\$floor\}\) \/ 2\)\);/
    );
  });

  it('keeps the default pill at 2.75rem, and its icon-only form at 2.5rem below 768px', () => {
    expect(button).toMatch(/&--medium\s*\{\s*button\s*\{[^}]*min-height:\s*2\.75rem;/);
    expect(button).toMatch(
      /&--medium\s*\{[\s\S]*?&\.p-button-icon-only\s*\{[^}]*width:\s*2\.75rem;[^}]*height:\s*2\.75rem;/
    );
    const narrow = button.slice(button.indexOf('@media (max-width: 768px)'));
    expect(narrow).toMatch(
      /&--medium button\s*\{[^{}]*&\.p-button-icon-only\s*\{[^}]*width:\s*2\.5rem;[^}]*height:\s*2\.5rem;/
    );
  });

  it('reaches past the pill with the shared hit area, with the ripple rounded on its own layer', () => {
    const base = button.slice(button.indexOf('.cleansia-button {'), button.indexOf('&--small'));

    expect(base).toMatch(/position:\s*relative;[\s\S]*?overflow:\s*visible;\s*@include touch-target;/);
    expect(base).toMatch(/&::before\s*\{[^}]*inset:\s*0;[^}]*border-radius:\s*inherit;/);
    expect(base).not.toMatch(/&::after/);
  });
});

