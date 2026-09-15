import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { CountryDetailDto } from '@cleansia/admin-services';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { CountryFormComponent } from './country-form.component';
import { CountryFormFacade } from './country-form.facade';

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly country = signal<CountryDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  loadCountry = jest.fn();
  createCountry = jest.fn();
  updateCountry = jest.fn();
  updateMarketContent = jest.fn();
  navigateBack = jest.fn();
}

describe('CountryFormComponent', () => {
  let fixture: ComponentFixture<CountryFormComponent>;
  let component: CountryFormComponent;
  let facade: FacadeStub;

  async function setup(mode: 'create' | 'edit'): Promise<void> {
    facade = new FacadeStub();
    const params: Record<string, string> = mode === 'edit' ? { countryId: 'country-1' } : {};

    await TestBed.configureTestingModule({
      imports: [CountryFormComponent, TranslateModule.forRoot()],
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
      .overrideComponent(CountryFormComponent, {
        add: { providers: [{ provide: CountryFormFacade, useValue: facade }] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(CountryFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  function detail(hasConfiguration: boolean, insuranceCoverageAmount?: number): CountryDetailDto {
    return CountryDetailDto.fromJS({
      id: 'country-1',
      isoCode: 'CZE',
      isoAlpha2: 'CZ',
      name: 'Czechia',
      isServiced: true,
      hasConfiguration,
      insuranceCoverageAmount,
    });
  }

  describe('create mode', () => {
    beforeEach(async () => {
      await setup('create');
    });

    it('uses OnPush change detection', () => {
      const meta = (CountryFormComponent as unknown as { ɵcmp: { onPush: boolean } }).ɵcmp;
      expect(meta.onPush).toBe(true);
    });

    it('requires the alpha-2 code on create', () => {
      component.form.patchValue({ isoCode: 'CZE', name: 'Czechia', isoAlpha2: '' });

      component.onSave();

      expect(facade.createCountry).not.toHaveBeenCalled();
      expect(component.form.controls.isoAlpha2.errors?.['required']).toBeTruthy();
    });

    it('refuses an alpha-2 code that is not exactly two letters', () => {
      component.form.patchValue({ isoCode: 'CZE', name: 'Czechia', isoAlpha2: 'CZE' });
      component.onSave();
      expect(facade.createCountry).not.toHaveBeenCalled();
      expect(component.form.controls.isoAlpha2.errors?.['pattern']).toBeTruthy();

      component.form.patchValue({ isoAlpha2: 'C1' });
      component.onSave();
      expect(facade.createCountry).not.toHaveBeenCalled();
    });

    it('creates with the alpha-2 code uppercased', () => {
      component.form.patchValue({ isoCode: 'CZE', name: 'Czechia', isoAlpha2: 'cz' });

      component.onSave();

      expect(facade.createCountry).toHaveBeenCalledWith({
        isoCode: 'CZE',
        isoAlpha2: 'CZ',
        name: 'Czechia',
      });
    });

    it('keeps the market section disabled with its hint — a new country has no configuration', () => {
      expect(component.marketContentEnabled()).toBe(false);
      const input = fixture.debugElement.query(By.css('[formControlName="insuranceCoverageAmount"] input'));
      expect(input.nativeElement.disabled).toBe(true);
      expect(fixture.nativeElement.textContent).toContain('pages.country_form.market_content_unavailable');
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup('edit');
    });

    it('loads the country', () => {
      expect(facade.loadCountry).toHaveBeenCalledWith('country-1');
    });

    it('disables the market field with the hint when the country has no configuration, and saves no market content', () => {
      facade.country.set(detail(false));
      fixture.detectChanges();

      expect(component.marketContentEnabled()).toBe(false);
      const input = fixture.debugElement.query(By.css('[formControlName="insuranceCoverageAmount"] input'));
      expect(input.nativeElement.disabled).toBe(true);
      expect(fixture.nativeElement.textContent).toContain('pages.country_form.market_content_unavailable');

      component.onSave();

      expect(facade.updateCountry).toHaveBeenCalledWith(
        'country-1',
        { isoCode: 'CZE', isoAlpha2: 'CZ', name: 'Czechia' },
        null
      );
    });

    it('enables the market field when the configuration exists and saves the ceiling with the country', () => {
      facade.country.set(detail(true, 1000000));
      fixture.detectChanges();

      expect(component.marketContentEnabled()).toBe(true);
      const input = fixture.debugElement.query(By.css('[formControlName="insuranceCoverageAmount"] input'));
      expect(input.nativeElement.disabled).toBe(false);
      expect(component.form.controls.insuranceCoverageAmount.value).toBe(1000000);

      component.form.controls.insuranceCoverageAmount.setValue('2000000' as unknown as number);
      component.onSave();

      expect(facade.updateCountry).toHaveBeenCalledWith(
        'country-1',
        { isoCode: 'CZE', isoAlpha2: 'CZ', name: 'Czechia' },
        { insuranceCoverageAmount: 2000000 }
      );
    });

    it('sends a cleared ceiling as null', () => {
      facade.country.set(detail(true, 1000000));
      fixture.detectChanges();

      component.form.controls.insuranceCoverageAmount.setValue('' as unknown as number);
      component.onSave();

      expect(facade.updateCountry).toHaveBeenCalledWith(
        'country-1',
        expect.anything(),
        { insuranceCoverageAmount: null }
      );
    });

    it('lets a legacy country without an alpha-2 code be renamed, and still refuses a malformed one', () => {
      facade.country.set(CountryDetailDto.fromJS({ ...detail(false), isoAlpha2: '' }));
      fixture.detectChanges();

      component.onSave();
      expect(facade.updateCountry).toHaveBeenCalledWith(
        'country-1',
        { isoCode: 'CZE', isoAlpha2: '', name: 'Czechia' },
        null
      );

      facade.updateCountry.mockClear();
      component.form.patchValue({ isoAlpha2: 'CZE' });
      component.onSave();
      expect(facade.updateCountry).not.toHaveBeenCalled();
    });
  });
});
