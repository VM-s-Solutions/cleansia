import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { CleansiaButtonComponent } from './cleansia-button.component';

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
      join(__dirname, '../../../../assets/src/styles/components/cleansia-button.component.scss'),
      'utf-8'
    );

    expect(scss).toMatch(/\.cleansia-button\.auto-width\s*\{[^}]*min-width:\s*0;/);
  });
});

