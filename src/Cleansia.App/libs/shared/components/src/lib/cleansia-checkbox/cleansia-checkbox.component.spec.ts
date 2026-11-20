import { TestBed } from '@angular/core/testing';
import { CleansiaCheckboxComponent } from './cleansia-checkbox.component';

describe('CleansiaCheckboxComponent (a11y)', () => {
  function render(inputs: Record<string, unknown>) {
    TestBed.configureTestingModule({ imports: [CleansiaCheckboxComponent] });
    const fixture = TestBed.createComponent(CleansiaCheckboxComponent);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    return element.querySelector('input[type=checkbox]') as HTMLInputElement;
  }

  it('names the box with its label', () => {
    const input = render({ id: 'remember-me', label: 'Remember me' });

    expect(input.id).toBe('remember-me');
    expect(Array.from(input.labels ?? []).map((l) => l.textContent?.trim())).toEqual(['Remember me']);
  });

  it('names the box with its label when no id is given', () => {
    const input = render({ label: 'Remember me' });

    expect(input.id).toMatch(/^cleansia-checkbox-/);
    expect(input.labels?.[0]?.textContent?.trim()).toBe('Remember me');
  });
});
