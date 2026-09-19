import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { CleansiaSelectComponent } from './cleansia-select.component';
import { ICleansiaSelectOption } from './cleansia-select.models';

const OPTIONS: ICleansiaSelectOption[] = [
  { label: 'Czechia', value: 'cze-id' },
  { label: 'Slovakia', value: 'svk-id' },
];

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, CleansiaSelectComponent],
  template: `<cleansia-select [formControl]="control" [options]="options" [showClear]="false" />`,
})
class ReactiveHostComponent {
  readonly control = new FormControl<string | null>(null);
  readonly options = OPTIONS;
}

@Component({
  standalone: true,
  imports: [FormsModule, CleansiaSelectComponent],
  template: `<cleansia-select [ngModel]="value()" [options]="options" [showClear]="false" />`,
})
class TemplateHostComponent {
  readonly value = signal<string | null>('cze-id');
  readonly options = OPTIONS;
}

/**
 * The select is OnPush and its value arrives through `writeValue`, which is not
 * an input — so a control set or disabled after the first render has to mark the
 * view itself, or the rendered label stays where it was. `NgModel` marks the
 * host view on its own after its asynchronous write; the reactive directives do
 * not, and the two cases below that name "after the first render" are the ones
 * that go red without the accessor's own mark.
 */
describe('CleansiaSelectComponent', () => {
  const renderedLabel = (fixture: ComponentFixture<unknown>): string =>
    (fixture.nativeElement as HTMLElement).querySelector('.p-select-label')?.textContent?.trim() ?? '';

  // Two rounds: the host's ngModel applies asynchronously, and so does the one
  // the select binds onto the PrimeNG control underneath it.
  const settle = async (fixture: ComponentFixture<unknown>): Promise<void> => {
    fixture.detectChanges();
    for (let round = 0; round < 2; round++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReactiveHostComponent, TemplateHostComponent],
      providers: [provideNoopAnimations()],
    });
  });

  it('renders the label of an initial ngModel value', async () => {
    const fixture = TestBed.createComponent(TemplateHostComponent);

    await settle(fixture);

    expect(renderedLabel(fixture)).toBe('Czechia');
  });

  it('renders the label of a value set on the control after the first render', async () => {
    const fixture = TestBed.createComponent(ReactiveHostComponent);
    await settle(fixture);
    expect(renderedLabel(fixture)).toBe('');

    fixture.componentInstance.control.setValue('svk-id');
    await settle(fixture);

    expect(renderedLabel(fixture)).toBe('Slovakia');
  });

  it('reflects a control disabled after the first render', async () => {
    const fixture = TestBed.createComponent(ReactiveHostComponent);
    await settle(fixture);

    fixture.componentInstance.control.disable();
    await settle(fixture);

    const combobox = (fixture.nativeElement as HTMLElement).querySelector('[role="combobox"]');
    expect(combobox?.getAttribute('aria-disabled')).toBe('true');
  });
});
