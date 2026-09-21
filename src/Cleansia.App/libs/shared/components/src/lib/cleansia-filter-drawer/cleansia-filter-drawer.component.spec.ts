import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { CleansiaFilterChipsComponent } from './cleansia-filter-chips.component';
import { CleansiaFilterDrawerComponent } from './cleansia-filter-drawer.component';
import { FilterDrawerState } from './filter-drawer-state';

@Component({
  standalone: true,
  imports: [CleansiaFilterDrawerComponent, CleansiaFilterChipsComponent],
  template: `
    <cleansia-filter-drawer [state]="state" title="Filtry">
      <p class="projected">fields</p>
    </cleansia-filter-drawer>
    <cleansia-filter-chips [state]="state" />
  `,
})
class HostComponent {
  readonly form = new FormBuilder().group({ searchTerm: [''], year: [null as number | null] });
  readonly apply = jest.fn();
  readonly state = new FilterDrawerState({
    form: this.form,
    lang: signal('cs'),
    chips: (value) => [
      ...(value.searchTerm ? [{ key: 'searchTerm', label: 'Hledat', value: value.searchTerm }] : []),
      ...(value.year ? [{ key: 'year', label: 'Rok', value: String(value.year) }] : []),
    ],
    apply: this.apply,
  });
}

describe('cleansia-filter-drawer', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let element: HTMLElement;

  beforeEach(async () => {
    jest.useFakeTimers();
    await TestBed.configureTestingModule({
      imports: [HostComponent, TranslateModule.forRoot()],
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    host.state.connect(new Subject<void>());
    element = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => jest.useRealTimers());

  const query = <T extends HTMLElement>(selector: string, root: ParentNode = element): T =>
    root.querySelector(selector) as T;
  const panel = () => query('[role="dialog"]');
  const trigger = () => query<HTMLButtonElement>('.cleansia-filter-drawer__trigger button');

  it('renders the panel as a modal dialog named after its title, inert while closed', () => {
    expect(panel()).not.toBeNull();
    expect(panel().getAttribute('aria-modal')).toBe('true');
    expect(panel().getAttribute('aria-label')).toBe('Filtry');
    expect(panel().hasAttribute('inert')).toBe(true);
    expect(element.querySelector('.projected')).not.toBeNull();
  });

  it('opens from the trigger and closes from the typed, labelled close button', () => {
    trigger().click();
    fixture.detectChanges();
    expect(host.state.isOpen()).toBe(true);
    expect(panel().hasAttribute('inert')).toBe(false);

    const close = query<HTMLButtonElement>('.cleansia-filter-drawer__close button', panel());
    expect(close.getAttribute('type')).toBe('button');
    expect(close.getAttribute('aria-label')).toBe('global.close');
    close.click();
    fixture.detectChanges();
    expect(host.state.isOpen()).toBe(false);
  });

  it('closes on Escape and on the backdrop', () => {
    host.state.open();
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(host.state.isOpen()).toBe(false);

    host.state.open();
    fixture.detectChanges();
    query('.cleansia-filter-drawer__backdrop').click();
    fixture.detectChanges();
    expect(host.state.isOpen()).toBe(false);
  });

  it('resets from the footer', () => {
    host.form.patchValue({ searchTerm: 'anna' });
    jest.advanceTimersByTime(500);
    fixture.detectChanges();
    query<HTMLButtonElement>('.cleansia-filter-drawer__footer button', panel()).click();
    fixture.detectChanges();
    expect(host.form.value.searchTerm).toBe('');
    expect(host.apply).toHaveBeenCalledWith({ searchTerm: '', year: null });
  });

  it('shows the count on the trigger only while filters are active', () => {
    expect(element.querySelector('.cleansia-filter-drawer__count')).toBeNull();
    host.form.patchValue({ searchTerm: 'anna', year: 2026 });
    fixture.detectChanges();
    expect(element.querySelector('.cleansia-filter-drawer__count')?.textContent?.trim()).toBe('2');
  });

  it('renders one typed chip-remove button per chip, labelled with the chip text, and clear-all', () => {
    host.form.patchValue({ searchTerm: 'anna', year: 2026 });
    fixture.detectChanges();

    const removes = element.querySelectorAll<HTMLButtonElement>('.cleansia-filter-chips__remove');
    expect(removes.length).toBe(2);
    expect(removes[0].getAttribute('type')).toBe('button');
    expect(removes[0].getAttribute('aria-label')).toBe('Hledat: anna');

    removes[1].click();
    fixture.detectChanges();
    expect(host.form.value.year).toBeNull();
    expect(host.form.value.searchTerm).toBe('anna');

    const clearAll = query<HTMLButtonElement>('.cleansia-filter-chips__clear');
    expect(clearAll.getAttribute('type')).toBe('button');
    clearAll.click();
    fixture.detectChanges();
    expect(host.form.value.searchTerm).toBe('');
    expect(element.querySelector('.cleansia-filter-chips__clear')).toBeNull();
  });
});
