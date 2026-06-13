import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
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
