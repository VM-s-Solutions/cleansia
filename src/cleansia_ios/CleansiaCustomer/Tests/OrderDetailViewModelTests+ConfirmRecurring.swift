import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
extension OrderDetailViewModelTests {
    // MARK: Confirm recurring

    func testConfirmRecurringCashNullSecretConfirmsAndRefetches() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 1)),
            .success(OrderFixtures.detail(statusValue: 2))
        ]
        client.confirmRecurringResult = .success(
            RecurringConfirmation(clientSecret: nil, stripeCustomerId: nil, ephemeralKey: nil)
        )
        let intent = FakePaymentIntentClient()
        let vm = makeVM(client: client, paymentIntent: intent)
        await vm.load()

        var presented: PaymentSheetPresentation?
        let cancellable = vm.recurringCardPayment.sink { presented = $0 }
        await vm.confirmRecurring()
        cancellable.cancel()

        XCTAssertNil(presented, "cash confirm must not present a PaymentSheet")
        XCTAssertEqual(client.confirmRecurringCallCount, 1)
        XCTAssertEqual(intent.callCount, 0, "a cash confirm asks for no intent")
        XCTAssertEqual(vm.confirmRecurringState, .idle)
    }

    /// Wire values: OrderStatus New = 1; PaymentType Cash = 1, Card = 2.
    func recurringOccurrence(paymentType: Int, needsConfirmation: Bool = true) -> CustomerOrderDetail {
        OrderFixtures.detail(
            statusCode: Code(type: "OrderStatus", name: nil, value: 1),
            needsConfirmation: needsConfirmation,
            paymentType: Code(type: "PaymentType", name: nil, value: paymentType),
            currencyCode: "CZK"
        )
    }

    /// ConfirmRecurring answers with its own intent and customer, which keep nothing and must not reach the
    /// sheet.
    func cardConfirmReady(
        _ intent: FakePaymentIntentClient,
        onCreditMoved: @escaping () -> Void = {}
    ) -> (OrderDetailViewModel, FakeOrderClient) {
        let client = FakeOrderClient()
        client.detailResults = [.success(recurringOccurrence(paymentType: 2))]
        client.confirmRecurringResult = .success(RecurringConfirmation(
            clientSecret: "pi_confirm_secret",
            stripeCustomerId: "cus_confirm",
            ephemeralKey: "ek_confirm"
        ))
        intent.result = .success(PaymentIntentDetails(
            clientSecret: "pi_secret",
            ephemeralKey: "ek_1",
            stripeCustomerId: "cus_1"
        ))
        return (makeVM(client: client, paymentIntent: intent, onCreditMoved: onCreditMoved), client)
    }

    func sheetsOpened(by vm: OrderDetailViewModel) async -> [PaymentSheetPresentation] {
        var sheets: [PaymentSheetPresentation] = []
        let cancellable = vm.recurringCardPayment.sink { sheets.append($0) }
        await vm.confirmRecurring()
        cancellable.cancel()
        return sheets
    }

    func testACardConfirmWithoutTheSaveTickOpensTheSheetWithoutTheCustomer() async {
        let intent = FakePaymentIntentClient()
        let (vm, client) = cardConfirmReady(intent)
        await vm.load()
        let detailCallsBefore = client.detailCallCount

        let sheets = await sheetsOpened(by: vm)

        XCTAssertEqual(intent.orderIds, ["o1"])
        XCTAssertEqual(intent.saveCards, [false])
        XCTAssertEqual(sheets.count, 1)
        XCTAssertEqual(sheets.first?.intentKind, .payment)
        XCTAssertEqual(sheets.first?.clientSecret, "pi_secret", "the sheet opens on CreatePaymentIntent's intent")
        XCTAssertEqual(sheets.first?.stripeCustomerId, "", "given the customer, PaymentSheet draws its own save box")
        XCTAssertEqual(sheets.first?.ephemeralKey, "", "given the customer, PaymentSheet draws its own save box")
        XCTAssertEqual(vm.confirmRecurringState, .idle)
        XCTAssertEqual(client.detailCallCount, detailCallsBefore, "an unpaid card occurrence is not read as confirmed")
    }

    func testATickedCardConfirmAsksTheIntentToSaveTheCardAndOpensTheSheetOnTheCustomer() async {
        let intent = FakePaymentIntentClient()
        let (vm, _) = cardConfirmReady(intent)
        await vm.load()

        vm.setSaveCard(true)
        let sheets = await sheetsOpened(by: vm)

        XCTAssertEqual(intent.saveCards, [true])
        XCTAssertEqual(sheets.count, 1)
        XCTAssertEqual(sheets.first?.clientSecret, "pi_secret")
        XCTAssertEqual(sheets.first?.stripeCustomerId, "cus_1")
        XCTAssertEqual(sheets.first?.ephemeralKey, "ek_1")
    }

    func testACardConfirmWhoseIntentIsRefusedOpensNoSheetAndLeavesTheButtonLive() async {
        let intent = FakePaymentIntentClient()
        let (vm, _) = cardConfirmReady(intent)
        intent.result = .failure(ApiError(httpStatus: 503))
        await vm.load()

        let sheets = await sheetsOpened(by: vm)

        XCTAssertEqual(intent.callCount, 1)
        XCTAssertTrue(sheets.isEmpty)
        XCTAssertEqual(vm.confirmRecurringState, .idle)
    }

    func testTheSaveTickIsOfferedUntickedOnlyWhileACardOccurrenceAwaitsItsConfirmation() async {
        func loaded(_ order: CustomerOrderDetail) async -> OrderDetailViewModel {
            let client = FakeOrderClient()
            client.detailResults = [.success(order)]
            let vm = makeVM(client: client)
            await vm.load()
            return vm
        }

        let card = await loaded(recurringOccurrence(paymentType: 2))
        XCTAssertTrue(card.offersCardSaving)
        XCTAssertFalse(card.saveCard)

        let confirmed = await loaded(recurringOccurrence(paymentType: 2, needsConfirmation: false))
        XCTAssertFalse(confirmed.offersCardSaving)

        let cash = await loaded(recurringOccurrence(paymentType: 1))
        XCTAssertFalse(cash.offersCardSaving)

        XCTAssertFalse(makeVM(client: FakeOrderClient()).offersCardSaving, "nothing is offered before the order loads")
    }

    /// The server takes the occurrence's credit when the confirmation is POSTed, before the intent exists,
    /// so the balance Rewards and Profile show is re-read then — whether or not the sheet is completed.
    func testACardConfirmRereadsTheCreditItSpentBeforeTheSheetOpens() async {
        var creditReads = 0
        let intent = FakePaymentIntentClient()
        let (vm, _) = cardConfirmReady(intent, onCreditMoved: { creditReads += 1 })
        await vm.load()

        _ = await sheetsOpened(by: vm)

        XCTAssertEqual(creditReads, 1)
    }

    /// A cash occurrence spends no credit, and a refused confirmation spent nothing.
    func testACashOrRefusedConfirmLeavesTheCreditAlone() async {
        var creditReads = 0
        for result: ApiResult<RecurringConfirmation> in [
            .success(RecurringConfirmation(clientSecret: nil, stripeCustomerId: nil, ephemeralKey: nil)),
            .failure(ApiError(httpStatus: 500))
        ] {
            let client = FakeOrderClient()
            client.detailResults = [.success(recurringOccurrence(paymentType: 1))]
            client.confirmRecurringResult = result
            let vm = makeVM(client: client, onCreditMoved: { creditReads += 1 })
            await vm.load()

            await vm.confirmRecurring()
        }

        XCTAssertEqual(creditReads, 0)
    }

    /// A debt refuses the confirmation whatever the tender: the screen offers the way to pay it instead of a
    /// snackbar, and no card payment is started.
    func testAConfirmRefusedForAnUnpaidAmountOffersTheWayToPayIt() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(recurringOccurrence(paymentType: 2))]
        client.confirmRecurringResult = .failure(ApiError(code: "order.unpaid_receivable", httpStatus: 400))
        let intent = FakePaymentIntentClient()
        let snackbar = SnackbarController()
        let vm = makeVM(client: client, paymentIntent: intent, snackbar: snackbar)
        await vm.load()

        await vm.confirmRecurring()

        XCTAssertTrue(vm.owesMoney)
        XCTAssertEqual(vm.confirmRecurringState, .idle)
        XCTAssertNil(snackbar.current)
        XCTAssertEqual(intent.callCount, 0)

        vm.dismissOwesMoney()
        XCTAssertFalse(vm.owesMoney)
    }

    func testConfirmRecurringFailureStaysIdle() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 1))]
        client.confirmRecurringResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM(client: client)
        await vm.load()

        await vm.confirmRecurring()

        XCTAssertEqual(vm.confirmRecurringState, .idle)
    }

    func testRecurringCardPaymentCompletedRefetches() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 1)),
            .success(OrderFixtures.detail(statusValue: 2))
        ]
        let vm = makeVM(client: client)
        await vm.load()
        let before = client.detailCallCount

        await vm.notifyRecurringPaymentResult(.completed)

        XCTAssertGreaterThan(client.detailCallCount, before, "completed PaymentSheet re-reads the order")
    }
}
