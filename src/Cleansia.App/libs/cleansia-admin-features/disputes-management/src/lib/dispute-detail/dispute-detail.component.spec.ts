import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  AdminDisputeClient,
  AdminOrderClient,
  DisputeDetails,
  DisputeSettlementPreference,
  DisputeStatus,
  OrderItem,
  PaymentType,
} from '@cleansia/admin-services';
import { PermissionService, Policy, SnackbarService } from '@cleansia/services';
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
  let orderClient: { details: jest.Mock };
  let deniedPolicies: Set<string>;

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
    deniedPolicies = new Set();
    orderClient = {
      details: jest.fn().mockReturnValue(of(OrderItem.fromJS({ assignedEmployees: [] }))),
    };

    await TestBed.configureTestingModule({
      imports: [DisputeDetailComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: AdminDisputeClient, useValue: disputeClient },
        { provide: AdminOrderClient, useValue: orderClient },
        { provide: PermissionService, useValue: { hasPolicy: (p: string) => !deniedPolicies.has(p) } },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
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
    expect(el.querySelector('.detail-grid')).toBeTruthy();
  });

  it('shows the reason in words, never its code name', () => {
    disputeClient.details.mockReturnValue(
      of(DisputeDetails.fromJS({ id: 'dispute-1', reason: { name: 'ServiceIncomplete', value: 3 }, messages: [] }))
    );
    const text = setup().el.textContent;

    expect(text).toContain('enums.dispute_reason.service_incomplete');
    expect(text).not.toContain('ServiceIncomplete');
  });

  describe('evidence and the order', () => {
    const withEvidence = (evidence: Record<string, unknown>[]) =>
      disputeClient.details.mockReturnValue(
        of(DisputeDetails.fromJS({ id: 'dispute-1', orderId: 'order-1', displayOrderNumber: 'ORD-1', evidence, messages: [] }))
      );
    const evidenceSection = (el: HTMLElement) => el.querySelector('.cleansia-dispute-detail__evidence') as HTMLElement;

    it('shows an image as a thumbnail that opens in a new tab', () => {
      withEvidence([{ id: 'e-1', fileName: 'kitchen.jpg', filePath: 'dispute-1/a1.jpg', blobUrl: 'https://blob/a1.jpg?sas' }]);
      const section = evidenceSection(setup().el);

      const link = section.querySelector('a[target="_blank"]') as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('https://blob/a1.jpg?sas');
      expect(link.querySelector('img')?.getAttribute('src')).toBe('https://blob/a1.jpg?sas');
    });

    it('chooses by the stored path, not the name the customer gave the file', () => {
      withEvidence([{ id: 'e-1', fileName: 'photo.jpg', filePath: 'dispute-1/a1.pdf', blobUrl: 'https://blob/a1.pdf?sas' }]);
      const section = evidenceSection(setup().el);

      expect(section.querySelector('a[target="_blank"]')?.getAttribute('href')).toBe('https://blob/a1.pdf?sas');
      expect(section.querySelector('img')).toBeNull();
      expect(section.textContent).toContain('photo.jpg');
    });

    it('names an item whose link failed and asks for a reload, never claiming no evidence', () => {
      withEvidence([{ id: 'e-1', fileName: 'kitchen.jpg', filePath: 'dispute-1/a1.jpg' }]);
      const section = evidenceSection(setup().el);

      expect(section.textContent).toContain('kitchen.jpg');
      expect(section.textContent).toContain('pages.disputes_management.detail.evidence_unavailable');
      expect(section.textContent).not.toContain('pages.disputes_management.detail.no_evidence');
      expect(section.querySelector('a')).toBeNull();
    });

    it('says there is no evidence when the customer sent none', () => {
      withEvidence([]);

      expect(evidenceSection(setup().el).textContent).toContain('pages.disputes_management.detail.no_evidence');
    });

    it('links the order number to the order page for an admin who may open it', () => {
      withEvidence([]);
      const link = setup().el.querySelector('.detail-identity a') as HTMLAnchorElement;

      expect(link.getAttribute('href')).toBe('/order-management/order-1');
      expect(link.textContent).toContain('ORD-1');
    });

    it('shows the order number as plain text to an admin who may not open the order', () => {
      deniedPolicies.add(Policy.CanViewOrderDetailAdmin);
      withEvidence([]);
      const identity = setup().el.querySelector('.detail-identity') as HTMLElement;

      expect(identity.querySelector('a')).toBeNull();
      expect(identity.textContent).toContain('ORD-1');
    });
  });

  it('shows what the resolution moved beside what it asked for', () => {
    disputeClient.details.mockReturnValue(
      of(
        DisputeDetails.fromJS({
          id: 'dispute-1',
          refundAmount: 1250,
          cardRefundedAmount: 1000,
          creditReturnedAmount: 150,
          currency: { code: 'CZK' },
          messages: [],
        })
      )
    );
    const text = setup().el.textContent;

    expect(text).toContain('pages.disputes_management.detail.refund_requested');
    expect(text).toContain('pages.disputes_management.detail.card_refunded');
    expect(text).toContain('pages.disputes_management.detail.credit_returned');
  });

  it('shows no moved-money rows on a dispute that never recorded them', () => {
    disputeClient.details.mockReturnValue(
      of(
        DisputeDetails.fromJS({
          id: 'dispute-1',
          refundAmount: 1250,
          currency: { code: 'CZK' },
          messages: [],
        })
      )
    );
    const text = setup().el.textContent;

    expect(text).toContain('pages.disputes_management.detail.refund_requested');
    expect(text).not.toContain('pages.disputes_management.detail.card_refunded');
    expect(text).not.toContain('pages.disputes_management.detail.credit_returned');
  });

  it('renders the error state when the load fails', () => {
    disputeClient.details.mockReturnValue(throwError(() => new Error('x')));
    const { facade, el } = setup();

    expect(facade.hasError()).toBe(true);
    expect(el.querySelector('.not-found-state')).toBeTruthy();
  });

  describe('resolving', () => {
    const openDispute = (settlementPreference: DisputeSettlementPreference) =>
      DisputeDetails.fromJS({
        id: 'dispute-1',
        orderId: 'order-1',
        status: { type: 'DisputeStatus', name: 'Pending', value: DisputeStatus.Pending },
        settlementPreference,
        messages: [],
      });

    const withCrew = () =>
      orderClient.details.mockReturnValue(
        of(
          OrderItem.fromJS({
            assignedEmployees: [{ id: 'seat-1', employeeId: 'emp-1', fullName: 'Jana Nová' }],
          })
        )
      );

    it('shows that the customer chose credit, and that the amount goes to their balance', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.Credit)));
      const text = setup().el.textContent;

      expect(text).toContain('pages.disputes_management.resolve.settlement_preference');
      expect(text).toContain('pages.disputes_management.resolve.settlement.credit');
      expect(text).toContain('pages.disputes_management.resolve.credit_disclaimer');
      expect(text).not.toContain('pages.disputes_management.resolve.settlement.card_refund');
      expect(text).not.toContain('pages.disputes_management.resolve.refund_disclaimer');
    });

    it('shows that the customer chose a card refund', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      const text = setup().el.textContent;

      expect(text).toContain('pages.disputes_management.resolve.settlement.card_refund');
      expect(text).toContain('pages.disputes_management.resolve.refund_disclaimer');
      expect(text).not.toContain('pages.disputes_management.resolve.settlement.credit');
      expect(text).not.toContain('pages.disputes_management.resolve.credit_disclaimer');
    });

    const paidBy = (paymentType: PaymentType) =>
      orderClient.details.mockReturnValue(of(OrderItem.fromJS({ paymentType: { value: paymentType }, assignedEmployees: [] })));

    it('reads a card refund on a cash order as money back, and warns that an amount will be refused', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      paidBy(PaymentType.Cash);
      const text = setup().el.textContent;

      expect(text).toContain('pages.disputes_management.resolve.settlement.cash_refund');
      expect(text).toContain('pages.disputes_management.resolve.cash_warning');
      expect(text).not.toContain('pages.disputes_management.resolve.refund_disclaimer');
    });

    it('keeps the credit wording on a cash order, with no warning', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.Credit)));
      paidBy(PaymentType.Cash);
      const text = setup().el.textContent;

      expect(text).toContain('pages.disputes_management.resolve.settlement.credit');
      expect(text).not.toContain('pages.disputes_management.resolve.cash_warning');
    });

    it('reads a card refund on a card order as a card refund', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      paidBy(PaymentType.Card);
      const text = setup().el.textContent;

      expect(text).toContain('pages.disputes_management.resolve.settlement.card_refund');
      expect(text).not.toContain('pages.disputes_management.resolve.cash_warning');
    });

    it('shows the choice on a settled dispute to an admin who cannot resolve', () => {
      deniedPolicies.add(Policy.CanResolveDispute);
      disputeClient.details.mockReturnValue(
        of(
          DisputeDetails.fromJS({
            id: 'dispute-1',
            orderId: 'order-1',
            status: { type: 'DisputeStatus', name: 'Resolved', value: DisputeStatus.Resolved },
            settlementPreference: DisputeSettlementPreference.CardRefund,
            messages: [],
          })
        )
      );
      withCrew();
      const text = setup().el.textContent;

      expect(orderClient.details).toHaveBeenCalledWith('order-1');
      expect(component.crewOptions()).toEqual([]);
      expect(text).toContain('pages.disputes_management.resolve.settlement_preference');
      expect(text).toContain('pages.disputes_management.resolve.settlement.card_refund');
    });

    it('offers a charge to the cleaner only when the order has a crew', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      expect(setup().el.textContent).not.toContain('pages.disputes_management.resolve.charge.toggle');

      fixture.destroy();
      withCrew();
      expect(setup().el.textContent).toContain('pages.disputes_management.resolve.charge.toggle');
      expect(component.crewOptions()).toEqual([{ label: 'Jana Nová', value: 'emp-1' }]);
    });

    it('does not resolve while a charge to the cleaner is incomplete', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      withCrew();
      setup();

      component.onChargeCleanerToggled(true);
      component.resolveForm.patchValue({ resolutionNotes: 'partly refunded', charge: { employeeId: 'emp-1' } });
      component.submitResolve();

      expect(disputeClient.resolve).not.toHaveBeenCalled();
      expect(TestBed.inject(SnackbarService).showErrorTranslated).toHaveBeenCalledWith(
        'pages.disputes_management.resolve.charge.incomplete'
      );
    });

    it.each([
      ['blank', '', false],
      ['whitespace', '   ', false],
      ['blank with a charge', '', true],
      ['whitespace with a charge', '  ', true],
    ])('does not resolve with %s notes, and says notes are required', (_case, notes, charging) => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      withCrew();
      setup();

      component.onChargeCleanerToggled(charging);
      component.resolveForm.patchValue({
        resolutionNotes: notes,
        charge: { employeeId: 'emp-1', amount: 150, reason: 'skipped the bathroom' },
      });
      component.submitResolve();

      expect(disputeClient.resolve).not.toHaveBeenCalled();
      expect(TestBed.inject(SnackbarService).showErrorTranslated).toHaveBeenCalledWith(
        'pages.disputes_management.resolve.notes_required'
      );
    });

    it('resolves with the charge to the cleaner once it is complete', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      disputeClient.resolve.mockReturnValue(of(undefined));
      withCrew();
      setup();

      component.onChargeCleanerToggled(true);
      component.resolveForm.patchValue({
        refundAmount: 500,
        resolutionNotes: 'partly refunded',
        charge: { employeeId: 'emp-1', amount: 150, reason: 'skipped the bathroom' },
      });
      component.submitResolve();

      expect(disputeClient.resolve.mock.calls[0][0].toJSON()).toEqual({
        disputeId: 'dispute-1',
        refundAmount: 500,
        resolutionNotes: 'partly refunded',
        chargeToCleaner: { employeeId: 'emp-1', amount: 150, reason: 'skipped the bathroom' },
      });
    });

    it('resolves without a charge when none is asked for, whatever the charge fields hold', () => {
      disputeClient.details.mockReturnValue(of(openDispute(DisputeSettlementPreference.CardRefund)));
      disputeClient.resolve.mockReturnValue(of(undefined));
      withCrew();
      setup();

      component.resolveForm.patchValue({ resolutionNotes: 'refunded', charge: { employeeId: 'emp-1' } });
      component.submitResolve();

      expect(disputeClient.resolve.mock.calls[0][0].chargeToCleaner).toBeUndefined();
    });
  });
});
