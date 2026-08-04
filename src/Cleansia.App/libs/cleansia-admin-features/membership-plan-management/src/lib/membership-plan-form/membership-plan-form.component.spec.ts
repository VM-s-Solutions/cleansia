import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { By } from '@angular/platform-browser';
import { TranslateModule } from '@ngx-translate/core';
import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { MembershipPlanFormComponent } from './membership-plan-form.component';
import { MembershipPlanFormFacade } from './membership-plan-form.facade';

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly plan = signal<unknown>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  loadPlan = jest.fn();
  create = jest.fn();
  update = jest.fn();
  navigateBack = jest.fn();
}

describe('MembershipPlanFormComponent', () => {
  let fixture: ComponentFixture<MembershipPlanFormComponent>;
  let component: MembershipPlanFormComponent;
  let facade: FacadeStub;

  function fillValidPlan(): void {
    component.form.patchValue({
      code: 'PLUS_MONTHLY',
      name: 'Cleansia Plus',
      monthlyPriceCzk: 199,
      stripePriceId: 'price_123',
    });
  }

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [MembershipPlanFormComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { data: {}, paramMap: { get: () => null } } },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideComponent(MembershipPlanFormComponent, {
        add: {
          providers: [{ provide: MembershipPlanFormFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(MembershipPlanFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('uses OnPush change detection', () => {
    const meta = (
      MembershipPlanFormComponent as unknown as { ɵcmp: { onPush: boolean } }
    ).ɵcmp;
    expect(meta.onPush).toBe(true);
  });

  it('renders an input bound to the express-upgrade quota', () => {
    const quotaInput = fixture.debugElement.query(
      By.css('[formControlName="expressUpgradesPerMonth"]')
    );

    expect(quotaInput).toBeTruthy();
  });

  it('sends the express quota an admin set on a new plan', () => {
    fillValidPlan();
    component.form.controls.expressUpgradesPerMonth.setValue(4);

    component.onSave();

    expect(facade.create).toHaveBeenCalledWith(
      expect.objectContaining({ expressUpgradesPerMonth: 4 })
    );
  });

  it('refuses to save a negative express quota', () => {
    fillValidPlan();
    component.form.controls.expressUpgradesPerMonth.setValue(-1);

    component.onSave();

    expect(facade.create).not.toHaveBeenCalled();
    expect(component.form.controls.expressUpgradesPerMonth.errors?.['min']).toBeTruthy();
  });
});
