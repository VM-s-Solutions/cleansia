import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminCurrencyDetailDto } from '@cleansia/admin-services';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { CurrencyFormComponent } from './currency-form.component';
import { CurrencyFormFacade } from './currency-form.facade';

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly currency = signal<AdminCurrencyDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  loadCurrency = jest.fn();
  createCurrency = jest.fn();
  updateCurrency = jest.fn();
  navigateBack = jest.fn();
}

describe('CurrencyFormComponent — the referral credit', () => {
  let fixture: ComponentFixture<CurrencyFormComponent>;
  let component: CurrencyFormComponent;
  let facade: FacadeStub;

  async function setup(mode: 'create' | 'edit'): Promise<void> {
    facade = new FacadeStub();
    const params: Record<string, string> = mode === 'edit' ? { currencyId: 'cur-czk' } : {};

    await TestBed.configureTestingModule({
      imports: [CurrencyFormComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { mode },
              paramMap: { get: (key: string) => params[key] ?? null },
            },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideComponent(CurrencyFormComponent, {
        add: { providers: [{ provide: CurrencyFormFacade, useValue: facade }] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(CurrencyFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  // UpdateCurrency reads a missing figure as null and clears it, so an edit that does not carry
  // the stored credit back would silently stop every referral in the currency paying.
  it('prefills the stored credit and sends it back on save', async () => {
    await setup('edit');
    facade.currency.set(
      AdminCurrencyDetailDto.fromJS({
        id: 'cur-czk',
        code: 'CZK',
        symbol: 'Kč',
        name: 'Czech koruna',
        noShowCredit: 250,
        referralCredit: 150,
      })
    );
    fixture.detectChanges();

    expect(component.form.controls.referralCredit.value).toBe(150);
    expect(fixture.nativeElement.textContent).toContain('pages.currency_form.referral_credit');

    component.onSave();

    expect(facade.updateCurrency).toHaveBeenCalledWith(
      'cur-czk',
      expect.objectContaining({ noShowCredit: 250, referralCredit: 150 })
    );
  });

  it('creates with a typed credit, and with none when left blank', async () => {
    await setup('create');
    component.form.patchValue({ code: 'EUR', symbol: '€', name: 'Euro', referralCredit: 6 });
    component.onSave();
    component.form.patchValue({ referralCredit: null });
    component.onSave();

    expect(facade.createCurrency.mock.calls[0][0].referralCredit).toBe(6);
    expect(facade.createCurrency.mock.calls[1][0].referralCredit).toBeNull();
  });

  it('accepts zero, which pays none, and refuses a negative figure', async () => {
    await setup('create');
    component.form.patchValue({ code: 'EUR', symbol: '€', name: 'Euro', referralCredit: 0 });
    expect(component.form.controls.referralCredit.valid).toBe(true);

    component.form.patchValue({ referralCredit: -1 });
    component.onSave();

    expect(component.form.controls.referralCredit.errors?.['min']).toBeTruthy();
    expect(facade.createCurrency).not.toHaveBeenCalled();
  });
});
