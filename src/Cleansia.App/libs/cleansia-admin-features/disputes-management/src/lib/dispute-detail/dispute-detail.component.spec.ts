import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import {
  AdminDisputeClient,
  DisputeDetails,
  DisputeStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { NEVER, of, throwError } from 'rxjs';
import { DisputeDetailComponent } from './dispute-detail.component';
import { DisputeDetailFacade } from './dispute-detail.facade';

describe('DisputeDetailComponent', () => {
  let component: DisputeDetailComponent;
  let fixture: ComponentFixture<DisputeDetailComponent>;
  let disputeClient: {
    details: jest.Mock;
    resolve: jest.Mock;
    updateStatus: jest.Mock;
    addMessage: jest.Mock;
  };

  function setup(): { facade: DisputeDetailFacade; el: HTMLElement } {
    fixture = TestBed.createComponent(DisputeDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    return {
      facade: fixture.debugElement.injector.get(DisputeDetailFacade),
      el: fixture.nativeElement,
    };
  }

  beforeEach(async () => {
    disputeClient = {
      details: jest.fn(),
      resolve: jest.fn(),
      updateStatus: jest.fn(),
      addMessage: jest.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [DisputeDetailComponent, TranslateModule.forRoot()],
      providers: [
        { provide: AdminDisputeClient, useValue: disputeClient },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: new Map([['disputeId', 'dispute-1']]) } },
        },
      ],
    })
      .overrideComponent(DisputeDetailComponent, {
        set: { providers: [DisputeDetailFacade] },
      })
      .compileComponents();
  });

  it('should create and load the dispute on init', () => {
    disputeClient.details.mockReturnValue(
      of(DisputeDetails.fromJS({ id: 'dispute-1', messages: [] }))
    );
    setup();

    expect(component).toBeTruthy();
    expect(disputeClient.details).toHaveBeenCalledWith('dispute-1');
  });

  it('renders the loading state', () => {
    disputeClient.details.mockReturnValue(NEVER);
    const { facade, el } = setup();

    expect(facade.loading()).toBe(true);
    expect(el.querySelector('cleansia-loader')).toBeTruthy();
  });

  it('renders the loaded state with the summary section', () => {
    disputeClient.details.mockReturnValue(
      of(
        DisputeDetails.fromJS({
          id: 'dispute-1',
          displayOrderNumber: 'ORD-1',
          status: { type: 'DisputeStatus', name: 'Pending', value: DisputeStatus.Pending },
          messages: [],
        })
      )
    );
    const { facade, el } = setup();

    expect(facade.dispute()?.id).toBe('dispute-1');
    expect(el.querySelector('.cleansia-dispute-detail__grid')).toBeTruthy();
  });

  it('renders the error state when the load fails', () => {
    disputeClient.details.mockReturnValue(throwError(() => new Error('x')));
    const { facade, el } = setup();

    expect(facade.hasError()).toBe(true);
    expect(el.querySelector('.cleansia-dispute-detail__state')).toBeTruthy();
  });
});
