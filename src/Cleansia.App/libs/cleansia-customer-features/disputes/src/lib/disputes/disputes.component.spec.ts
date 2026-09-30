import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  DisputeDetails,
  DisputeListItem,
  DisputeReason,
  DisputeSettlementPreference,
  OrderListItem,
  PaymentType,
} from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
import { DisputesComponent } from './disputes.component';
import { DisputesFacade } from './disputes.facade';

const CARD_ORDER_ID = 'order-card';
const CASH_ORDER_ID = 'order-cash';
const DESCRIPTION = 'The kitchen was not cleaned at all.';

const order = (id: string, paymentType: PaymentType) =>
  OrderListItem.fromJS({
    id,
    displayOrderNumber: id,
    paymentType: { value: paymentType, name: PaymentType[paymentType] },
    selectedServices: [],
    selectedPackages: [],
  });

class FakeDisputesFacade {
  disputes = signal<DisputeListItem[]>([]);
  totalRecords = signal(0);
  loading = signal(false);
  disputeDetail = signal<DisputeDetails | undefined>(undefined);
  detailLoading = signal(false);
  orders = signal<OrderListItem[]>([
    order(CARD_ORDER_ID, PaymentType.Card),
    order(CASH_ORDER_ID, PaymentType.Cash),
  ]);
  orderOptions = computed(() => this.orders().map((o) => ({ label: `#${o.id}`, value: o.id })));
  sendingMessage = signal(false);
  creatingDispute = signal(false);
  uploadingEvidence = signal(false);
  unreadDisputeIds = signal<ReadonlySet<string>>(new Set());
  loadDisputes = jest.fn();
  loadOrdersForSelect = jest.fn();
  markViewed = jest.fn();
  loadDisputeDetail = jest.fn();
  createDispute = jest.fn();
  uploadEvidence = jest.fn();
  reportEvidenceOrphaned = jest.fn();
  sendMessage = jest.fn();
}

/**
 * A justified complaint is settled by a refund unless the customer chose credit: credit expires and is
 * never paid out, so the form must open on the refund and carry a credit choice through untouched. A
 * cash order has no card, so it must not be promised one.
 */
describe('DisputesComponent — how a justified complaint is settled', () => {
  let fixture: ComponentFixture<DisputesComponent>;
  let component: DisputesComponent;
  let facade: FakeDisputesFacade;

  beforeEach(async () => {
    facade = new FakeDisputesFacade();
    await TestBed.configureTestingModule({
      imports: [DisputesComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: { get: () => null } } },
        },
      ],
    })
      .overrideComponent(DisputesComponent, {
        set: { providers: [{ provide: DisputesFacade, useValue: facade }] },
      })
      .compileComponents();
    fixture = TestBed.createComponent(DisputesComponent);
    component = fixture.componentInstance;
  });

  const settlement = () => component.createForm.controls.settlementPreference;

  const fillValidForm = (orderId: string) =>
    component.createForm.patchValue({ orderId, description: DESCRIPTION });

  const optionLabels = () => component.settlementOptions().map((option) => option.label);

  it('opens on a refund, not credit', () => {
    expect(settlement().value).toBe(DisputeSettlementPreference.CardRefund);
  });

  it('goes back to a refund when the form is left after choosing credit', () => {
    settlement().setValue(DisputeSettlementPreference.Credit);

    component.cancelNew();

    expect(settlement().value).toBe(DisputeSettlementPreference.CardRefund);
  });

  it('files a refund when the customer did not touch the choice', () => {
    fillValidForm(CARD_ORDER_ID);

    component.createDispute();

    expect(facade.createDispute).toHaveBeenCalledWith(
      CARD_ORDER_ID,
      DisputeReason.QualityIssue,
      DESCRIPTION,
      [],
      DisputeSettlementPreference.CardRefund,
      expect.any(Function),
    );
  });

  it('files credit when the customer chose it', () => {
    fillValidForm(CARD_ORDER_ID);
    settlement().setValue(DisputeSettlementPreference.Credit);

    component.createDispute();

    expect(facade.createDispute).toHaveBeenCalledWith(
      CARD_ORDER_ID,
      DisputeReason.QualityIssue,
      DESCRIPTION,
      [],
      DisputeSettlementPreference.Credit,
      expect.any(Function),
    );
  });

  it('offers a card refund and says where it goes on a card order', () => {
    component.createForm.controls.orderId.setValue(CARD_ORDER_ID);

    expect(optionLabels()).toEqual([
      'pages.disputes.settlement_card_refund',
      'pages.disputes.settlement_credit',
    ]);
    expect(component.settlementHintKey()).toBe('pages.disputes.settlement_hint');
  });

  it('promises no card on a cash order, and still opens on the refund rather than credit', () => {
    component.createForm.controls.orderId.setValue(CASH_ORDER_ID);

    expect(optionLabels()).toEqual([
      'pages.disputes.settlement_cash_refund',
      'pages.disputes.settlement_credit',
    ]);
    expect(component.settlementOptions()[0].value).toBe(DisputeSettlementPreference.CardRefund);
    expect(settlement().value).toBe(DisputeSettlementPreference.CardRefund);
    expect(component.settlementHintKey()).toBe('pages.disputes.settlement_hint_cash');
  });

  it('shows the cash hint, not the card one, in the form of a cash order', () => {
    component.startNew();
    component.createForm.controls.orderId.setValue(CASH_ORDER_ID);
    fixture.detectChanges();

    const hints = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cl-dsp__hint'),
    ).map((hint) => hint.textContent?.trim());
    expect(hints).toContain('pages.disputes.settlement_hint_cash');
    expect(hints).not.toContain('pages.disputes.settlement_hint');
  });
});
