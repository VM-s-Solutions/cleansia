import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminDisputeClient, DisputeListItem } from '@cleansia/admin-services';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { DisputesManagementComponent } from './disputes-management.component';
import { DisputesManagementFacade } from './disputes-management.facade';

describe('DisputesManagementComponent', () => {
  let component: DisputesManagementComponent;
  let fixture: ComponentFixture<DisputesManagementComponent>;
  let disputeClient: { getPaged: jest.Mock };

  function setup(): { facade: DisputesManagementFacade; el: HTMLElement } {
    fixture = TestBed.createComponent(DisputesManagementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    return {
      facade: fixture.debugElement.injector.get(DisputesManagementFacade),
      el: fixture.nativeElement,
    };
  }

  beforeEach(async () => {
    disputeClient = { getPaged: jest.fn() };

    await TestBed.configureTestingModule({
      imports: [DisputesManagementComponent, TranslateModule.forRoot()],
      providers: [{ provide: AdminDisputeClient, useValue: disputeClient }],
    })
      .overrideComponent(DisputesManagementComponent, {
        set: { providers: [DisputesManagementFacade] },
      })
      .compileComponents();
  });

  it('should create and load disputes on init', () => {
    disputeClient.getPaged.mockReturnValue(
      of({ data: [DisputeListItem.fromJS({ id: 'd-1' })], total: 1 })
    );
    setup();

    expect(component).toBeTruthy();
    expect(disputeClient.getPaged).toHaveBeenCalled();
  });

  it('renders the loaded state with the table', () => {
    disputeClient.getPaged.mockReturnValue(
      of({ data: [DisputeListItem.fromJS({ id: 'd-1' })], total: 1 })
    );
    const { facade, el } = setup();

    expect(facade.initialLoading()).toBe(false);
    expect(facade.hasError()).toBe(false);
    expect(el.querySelector('cleansia-table')).toBeTruthy();
  });

  it('renders the empty state with no rows', () => {
    disputeClient.getPaged.mockReturnValue(of({ data: [], total: 0 }));
    const { facade } = setup();

    expect(facade.disputes().length).toBe(0);
    expect(facade.hasError()).toBe(false);
  });

  it('renders the error state when the load fails', () => {
    disputeClient.getPaged.mockReturnValue(throwError(() => new Error('x')));
    const { facade, el } = setup();

    expect(facade.hasError()).toBe(true);
    expect(el.querySelector('.cleansia-disputes-management__state')).toBeTruthy();
  });
});
