import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { CleansiaTextInputComponent } from '../cleansia-text-input';
import { CleansiaTextareaComponent } from '../cleansia-textarea';

/**
 * The shape of the admin catalogue forms: a root description, one translation block per language
 * and one price block per currency, where both kinds of block are added to the group only once the
 * language and currency lists arrive.
 */
@Component({
  standalone: true,
  imports: [ReactiveFormsModule, CleansiaTextareaComponent, CleansiaTextInputComponent],
  template: `
    <form [formGroup]="form">
      <cleansia-textarea label="Description" formControlName="description" />
      <div formGroupName="translations">
        @for (lang of languages(); track lang) {
        <div [formGroupName]="lang">
          <cleansia-textarea label="Translated description" formControlName="description" />
        </div>
        }
      </div>
      <div formGroupName="prices">
        @for (code of currencies(); track code) {
        <cleansia-text-input [label]="code" [formControlName]="code" dataType="number" />
        }
      </div>
    </form>
  `,
})
class CatalogueFormHostComponent {
  private readonly fb = new FormBuilder();
  readonly languages = signal<string[]>([]);
  readonly currencies = signal<string[]>([]);
  readonly form = this.fb.nonNullable.group({
    description: ['', [Validators.maxLength(5)]],
    translations: this.fb.nonNullable.group({}),
    prices: this.fb.nonNullable.group({}),
  });

  addLanguages(codes: string[]): void {
    const translations = this.form.controls.translations as FormGroup;
    for (const code of codes) {
      translations.addControl(
        code,
        this.fb.nonNullable.group({ description: ['', [Validators.maxLength(5)]] })
      );
    }
    this.languages.set(codes);
  }

  addCurrencies(codes: string[]): void {
    const prices = this.form.controls.prices as FormGroup;
    for (const code of codes) {
      prices.addControl(code, new FormControl<number | null>(null, [Validators.min(0)]));
    }
    this.currencies.set(codes);
  }
}

describe('CleansiaBaseFormInputComponent — formControlName inside a nested group', () => {
  let fixture: ComponentFixture<CatalogueFormHostComponent>;
  let host: CatalogueFormHostComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CatalogueFormHostComponent, TranslateModule.forRoot()],
    });
    fixture = TestBed.createComponent(CatalogueFormHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  function textareas(): CleansiaTextareaComponent[] {
    return fixture.debugElement
      .queryAll(By.directive(CleansiaTextareaComponent))
      .map((de) => de.componentInstance as CleansiaTextareaComponent);
  }

  function textInputs(): CleansiaTextInputComponent[] {
    return fixture.debugElement
      .queryAll(By.directive(CleansiaTextInputComponent))
      .map((de) => de.componentInstance as CleansiaTextInputComponent);
  }

  it('renders a price block whose control name exists only in the nested group', () => {
    host.addCurrencies(['CZK', 'EUR']);

    expect(() => fixture.detectChanges()).not.toThrow();

    const prices = host.form.controls.prices as FormGroup;
    const [czk, eur] = textInputs();
    expect(czk.formControl).toBe(prices.get('CZK'));
    expect(eur.formControl).toBe(prices.get('EUR'));
  });

  it('binds a translation block to its own nested control, not the root one of the same name', () => {
    host.addLanguages(['cs', 'en']);
    fixture.detectChanges();

    const translations = host.form.controls.translations as FormGroup;
    const [root, cs, en] = textareas();
    expect(root.formControl).toBe(host.form.controls.description);
    expect(cs.formControl).toBe(translations.get('cs.description'));
    expect(en.formControl).toBe(translations.get('en.description'));
  });

  it('shows the nested control’s own error and leaves the root one clean', () => {
    host.addLanguages(['cs']);
    fixture.detectChanges();

    const blocks: NodeListOf<HTMLElement> =
      fixture.nativeElement.querySelectorAll('cleansia-textarea');
    const nested = blocks[1].querySelector('textarea') as HTMLTextAreaElement;
    nested.value = 'too long for five';
    nested.dispatchEvent(new Event('input'));
    nested.dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(host.form.controls.description.valid).toBe(true);
    expect(host.form.get('translations.cs.description')?.invalid).toBe(true);
    expect(blocks[0].querySelector('.cleansia-error-message-container')).toBeNull();
    expect(blocks[1].querySelector('.cleansia-error-message-container')).not.toBeNull();
  });
});
