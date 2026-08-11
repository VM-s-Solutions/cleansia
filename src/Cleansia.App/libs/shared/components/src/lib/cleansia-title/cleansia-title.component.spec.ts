import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { CleansiaTitleComponent } from './cleansia-title.component';

describe('CleansiaTitleComponent', () => {
  let fixture: ComponentFixture<CleansiaTitleComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaTitleComponent, TranslateModule.forRoot()],
    }).compileComponents();

    fixture = TestBed.createComponent(CleansiaTitleComponent);
    fixture.componentRef.setInput('title', 'Log In');
  });

  function heading(): HTMLElement {
    return fixture.nativeElement.querySelector('h1, h2, h3, h5');
  }

  it.each([
    ['small', 'H5'],
    ['default', 'H3'],
    ['big', 'H2'],
    ['large', 'H1'],
  ])(
    'derives the heading rank from size=%s when no level is given',
    (size, tag) => {
      fixture.componentRef.setInput('size', size);
      fixture.detectChanges();

      expect(heading().tagName).toBe(tag);
      expect(heading().className).toContain(`cleansia-title--${size}`);
    }
  );

  it('lets a page claim the h1 without changing its type scale', () => {
    fixture.componentRef.setInput('level', 1);
    fixture.detectChanges();

    expect(heading().tagName).toBe('H1');
    expect(heading().className).toContain('cleansia-title--default');
  });
});
