package cz.cleansia.customer.features.orders

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.consent.GdprConsentClient
import cz.cleansia.customer.core.consent.SignupConsentType
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.memberships.MembershipStatus
import cz.cleansia.customer.core.notifications.OrderEvent
import cz.cleansia.customer.core.notifications.OrderEventBus
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.ConfirmRecurringOrderResponse
import cz.cleansia.customer.core.orders.OrderCurrencyDetailDto
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.payments.CreatePaymentIntentResponse
import cz.cleansia.customer.core.payments.PaymentRepository
import cz.cleansia.customer.core.payments.PaymentSheetParams
import cz.cleansia.customer.core.user.CodeDto
import cz.cleansia.customer.features.recurring.RecurringAuthoringGate
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * Covers the two behaviours the Order detail screen depends on but that are
 * invisible to a compile check: the safety-net poller's arm/stop rule, and
 * `load()`'s decision to blank the screen or keep the current order on it.
 *
 * Two hard rules for anything added here:
 *
 *  1. The poller is a `while (true) { delay(...) }` inside `viewModelScope`,
 *     which is *not* a child of the `runTest` scope. `advanceUntilIdle()` while
 *     it is armed therefore never returns — it keeps draining an infinite
 *     stream of scheduled ticks. Use `runCurrent()` to settle the initial load
 *     and a bounded `advanceTimeBy(...)` to step ticks, then
 *     `vm.viewModelScope.cancel()` before the test ends.
 *  2. `advanceTimeBy` does not run work scheduled at exactly
 *     `currentTime + delayTime`, so stepping N ticks needs
 *     `N * POLL_INTERVAL_MS + 1`.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OrderDetailViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: OrderRepository
    private lateinit var membershipRepository: MembershipRepository
    private lateinit var membershipStaleness: Staleness
    private lateinit var membership: MutableStateFlow<GetMyMembershipResponse?>
    private val markets = MutableStateFlow<MarketState>(MarketState.Unavailable)
    private val marketRepository = mockk<MarketRepository> {
        every { state } returns markets
        coEvery { ensureLoaded() } answers { markets.value }
    }
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context
    private lateinit var orderEventBus: OrderEventBus
    private lateinit var paymentRepository: PaymentRepository
    private lateinit var loyaltyRepository: LoyaltyRepository
    private lateinit var consentClient: GdprConsentClient

    /** Mirrors the VM's private companion constant — 5 minutes. */
    private val pollIntervalMs = 5L * 60L * 1000L

    private val orderId = "order-1"

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        membershipStaleness = Staleness()
        membership = MutableStateFlow(null)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        orderEventBus = OrderEventBus()
        paymentRepository = mockk(relaxed = true)
        loyaltyRepository = mockk(relaxed = true)
        consentClient = mockk()
        coEvery { consentClient.grantedTypes() } returns
            setOf(SignupConsentType.TermsOfService, SignupConsentType.PrivacyPolicy)

        every { membershipRepository.current } returns membership
        every { membershipRepository.staleness } returns membershipStaleness
        coEvery { membershipRepository.refresh() } coAnswers { membershipAnswer(hasMembership = false) }
    }

    /** Stands in for the repository writing its cache from a successful fetch. */
    private fun membershipAnswer(hasMembership: Boolean, status: MembershipStatus? = null): ApiResult<GetMyMembershipResponse> {
        val body = GetMyMembershipResponse(hasMembership = hasMembership, status = status?.code)
        membership.value = body
        membershipStaleness.markFresh()
        return ApiResult.Success(body)
    }

    private fun viewModel(id: String? = orderId) = OrderDetailViewModel(
        orderRepository = repository,
        marketRepository = marketRepository,
        snackbar = snackbar,
        appContext = appContext,
        savedStateHandle = SavedStateHandle(mapOf("orderId" to id)),
        membershipRepository = membershipRepository,
        orderEventBus = orderEventBus,
        loyaltyRepository = loyaltyRepository,
        paymentRepository = paymentRepository,
        consentClient = consentClient,
    )

    /** Wire values: Confirmed=2, OnTheWay=3, InProgress=4, Completed=5, Cancelled=6. */
    private fun order(statusValue: Int, notes: String? = null) = OrderDetailDto(
        id = orderId,
        totalPrice = 4380.0,
        originalSubtotal = 3650.0,
        appliedDiscountSource = 2,
        notes = notes,
        orderStatus = CodeDto(type = "OrderStatus", name = "status-$statusValue", value = statusValue),
    )

    private fun loadedOrder(vm: OrderDetailViewModel): OrderDetailDto =
        (vm.state.value as OrderDetailUiState.Loaded).order

    // ── poller arm / stop rule ──

    @Test
    fun `poller keeps ticking while the order is InProgress`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(4))

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(2 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        // 1 initial load + 2 ticks. Before the fix the loop broke on the first
        // tick because InProgress (4) was missing from its keep-going list, so
        // an order polled exactly once and then never again for the whole clean.
        coVerify(exactly = 3) { repository.getById(orderId) }
    }

    @Test
    fun `poller keeps ticking while the order is OnTheWay`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(3))

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(2 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        coVerify(exactly = 3) { repository.getById(orderId) }
    }

    @Test
    fun `poller stops once the order reaches a terminal status`() = runTest {
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            ApiResult.Success(order(if (calls++ == 0) 2 else 5))
        }

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(3 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        // Initial load (Confirmed) arms the poller; the first tick returns
        // Completed, which must stop it. This pins the other half of the fix —
        // widening the keep-going condition must not turn it into a loop that
        // polls a finished order forever.
        coVerify(exactly = 2) { repository.getById(orderId) }
        assertEquals(5, loadedOrder(vm).orderStatus?.value)
    }

    @Test
    fun `poller never arms for a terminal order`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(3 * pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        coVerify(exactly = 1) { repository.getById(orderId) }
    }

    @Test
    fun `a poll tick that fails leaves the loaded order on screen`() = runTest {
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) {
                ApiResult.Success(order(4, notes = "original"))
            } else {
                ApiResult.Error(ApiError.Network("offline"))
            }
        }

        val vm = viewModel()
        runCurrent()

        advanceTimeBy(pollIntervalMs + 1)
        vm.viewModelScope.cancel()

        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
        assertEquals("original", loadedOrder(vm).notes)
    }

    // ── load() keeps content on a refetch ──

    @Test
    fun `a push-triggered refetch keeps the current order on screen`() = runTest {
        val gate = CompletableDeferred<ApiResult<OrderDetailDto>>()
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) ApiResult.Success(order(5, notes = "original")) else gate.await()
        }

        val vm = viewModel()
        advanceUntilIdle()
        assertTrue(vm.state.value is OrderDetailUiState.Loaded)

        orderEventBus.emit(OrderEvent(orderId = orderId, eventKey = "order.completed"))
        runCurrent()

        // The refetch is in flight. Before the fix `load()` set Loading
        // unconditionally, so every push blanked the whole screen to a
        // full-page spinner and threw away content that was still valid.
        assertTrue(
            "a background refetch must not blank the screen",
            vm.state.value is OrderDetailUiState.Loaded,
        )
        assertEquals("original", loadedOrder(vm).notes)

        gate.complete(ApiResult.Success(order(5, notes = "updated")))
        advanceUntilIdle()
        assertEquals("updated", loadedOrder(vm).notes)
    }

    @Test
    fun `a failed refetch keeps the loaded order instead of dropping to Error`() = runTest {
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) {
                ApiResult.Success(order(5, notes = "original"))
            } else {
                ApiResult.Error(ApiError.Server(statusCode = 500, message = "boom"))
            }
        }

        val vm = viewModel()
        advanceUntilIdle()

        orderEventBus.emit(OrderEvent(orderId = orderId, eventKey = "order.completed"))
        advanceUntilIdle()

        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
        assertEquals("original", loadedOrder(vm).notes)
    }

    @Test
    fun `the first load still shows the spinner`() = runTest {
        val gate = CompletableDeferred<ApiResult<OrderDetailDto>>()
        coEvery { repository.getById(orderId) } coAnswers { gate.await() }

        val vm = viewModel()
        runCurrent()
        assertEquals(OrderDetailUiState.Loading, vm.state.value)

        gate.complete(ApiResult.Success(order(5)))
        advanceUntilIdle()
        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
    }

    @Test
    fun `retrying from the Error state shows the spinner again`() = runTest {
        val gate = CompletableDeferred<ApiResult<OrderDetailDto>>()
        var calls = 0
        coEvery { repository.getById(orderId) } coAnswers {
            if (calls++ == 0) ApiResult.Error(ApiError.Network("offline")) else gate.await()
        }

        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(OrderDetailUiState.Error(canRetry = true), vm.state.value)

        // There is nothing on screen worth keeping here, so the retry must
        // still render a spinner rather than sit on a dead Error screen.
        vm.refresh()
        runCurrent()
        assertEquals(OrderDetailUiState.Loading, vm.state.value)

        gate.complete(ApiResult.Success(order(5)))
        advanceUntilIdle()
        assertTrue(vm.state.value is OrderDetailUiState.Loaded)
    }

    @Test
    fun `a missing nav arg is a fatal error and never fetches`() = runTest {
        val vm = viewModel(id = null)
        advanceUntilIdle()

        assertEquals(OrderDetailUiState.Error(canRetry = false), vm.state.value)
        coVerify(exactly = 0) { repository.getById(any()) }
    }

    // ── "Make this recurring" gate ──

    @Test
    fun `a resolved non-member loses the make-recurring shortcut`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Upsell, vm.recurringAuthoring.value)
    }

    /**
     * The defect: nothing on this screen fetched membership, so the shortcut was
     * withheld from paid-up members whenever no other screen had warmed the cache.
     * An answer that has not landed must not cost them the affordance — the server
     * refuses an unentitled create with its own localized message.
     */
    @Test
    fun `an unresolved membership keeps the make-recurring shortcut`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        coEvery { membershipRepository.refresh() } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Allowed, vm.recurringAuthoring.value)
    }

    @Test
    fun `a member keeps the make-recurring shortcut`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        coEvery { membershipRepository.refresh() } coAnswers { membershipAnswer(hasMembership = true) }

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Allowed, vm.recurringAuthoring.value)
    }

    /** A failed renewal keeps the enrolment alive, but the server refuses the schedule it would create. */
    @Test
    fun `a past-due member loses the make-recurring shortcut`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        coEvery { membershipRepository.refresh() } coAnswers {
            membershipAnswer(hasMembership = true, status = MembershipStatus.PastDue)
        }

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Paused, vm.recurringAuthoring.value)
    }

    @Test
    fun `the screen fetches membership itself rather than trusting another screen's cache`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))

        viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { membershipRepository.refresh() }
    }

    @Test
    fun `a membership answer still inside its freshness window is not re-fetched`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        membershipAnswer(hasMembership = true)

        viewModel()
        advanceUntilIdle()

        coVerify(exactly = 0) { membershipRepository.refresh() }
    }

    // ── recurring confirm ──

    /** Wire values: OrderStatus New = 1; PaymentType Cash = 1; PaymentStatus Pending = 1. */
    private fun recurringCashOccurrence(needsConfirmation: Boolean) = order(1).copy(
        recurringTemplateId = "tpl-1",
        paymentType = CodeDto(type = "PaymentType", name = "Cash", value = 1),
        paymentStatus = CodeDto(type = "PaymentStatus", name = "Pending", value = 1),
        needsConfirmation = needsConfirmation,
    )

    @Test
    fun `a cash confirm re-reads the occurrence, which no longer asks to be confirmed and is still unpaid`() = runTest {
        coEvery { repository.getById(orderId) } returnsMany listOf(
            ApiResult.Success(recurringCashOccurrence(needsConfirmation = true)),
            ApiResult.Success(recurringCashOccurrence(needsConfirmation = false)),
        )
        coEvery { repository.refresh() } returns ApiResult.Success(Unit)
        coEvery { repository.confirmRecurring(orderId, null) } returns
            ApiResult.Success(ConfirmRecurringOrderResponse(orderId = orderId))
        every { appContext.getString(R.string.recurring_confirm_success) } returns "Booking confirmed"

        val vm = viewModel()
        advanceUntilIdle()
        assertTrue(loadedOrder(vm).needsConfirmation)

        vm.confirmRecurring()
        advanceUntilIdle()

        assertEquals(false, loadedOrder(vm).needsConfirmation)
        assertEquals(1, loadedOrder(vm).paymentStatus?.value)
        assertEquals(ActionState.Idle, vm.confirmRecurringState.value)
        verify(exactly = 1) { snackbar.showSuccess("Booking confirmed") }
        coVerify(exactly = 2) { repository.getById(orderId) }
        coVerify(exactly = 0) { paymentRepository.createPaymentIntent(any(), any()) }
    }

    /** Wire value: PaymentType Card = 2. */
    private fun recurringCardOccurrence(needsConfirmation: Boolean = true) =
        recurringCashOccurrence(needsConfirmation).copy(
            paymentType = CodeDto(type = "PaymentType", name = "Card", value = 2),
            currency = OrderCurrencyDetailDto(code = "CZK"),
        )

    /** ConfirmRecurring answers with its own intent and customer, which keep nothing and must not reach the sheet. */
    private fun cardConfirmReady(): OrderDetailViewModel {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(recurringCardOccurrence())
        coEvery { repository.confirmRecurring(orderId, null) } returns ApiResult.Success(
            ConfirmRecurringOrderResponse(
                orderId = orderId,
                clientSecret = "pi_confirm_secret",
                paymentIntentId = "pi_confirm",
                stripeCustomerId = "cus_confirm",
                ephemeralKey = "ek_confirm",
            ),
        )
        coEvery { paymentRepository.createPaymentIntent(orderId, any()) } returns ApiResult.Success(
            CreatePaymentIntentResponse(
                clientSecret = "pi_secret",
                paymentIntentId = "pi_1",
                stripeCustomerId = "cus_1",
                ephemeralKey = "ek_1",
            ),
        )
        return viewModel()
    }

    private fun TestScope.sheetsOpened(vm: OrderDetailViewModel): List<PaymentSheetParams> {
        val sheets = mutableListOf<PaymentSheetParams>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { vm.cardPayment.collect { sheets += it } }
        return sheets
    }

    @Test
    fun `a card confirm without the save tick opens the sheet without the customer`() = runTest {
        val vm = cardConfirmReady()
        val sheets = sheetsOpened(vm)
        advanceUntilIdle()

        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 1) { paymentRepository.createPaymentIntent(orderId, false) }
        coVerify(exactly = 0) { paymentRepository.createPaymentIntent(any(), true) }
        val sheet = sheets.single()
        assertEquals("pi_secret", sheet.clientSecret)
        assertNull(sheet.customerId)
        assertNull(sheet.ephemeralKey)
        assertEquals(ActionState.Idle, vm.confirmRecurringState.value)
    }

    @Test
    fun `a ticked card confirm asks the intent to save the card and opens the sheet on the customer`() = runTest {
        val vm = cardConfirmReady()
        val sheets = sheetsOpened(vm)
        advanceUntilIdle()

        vm.setSaveCard(true)
        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 1) { paymentRepository.createPaymentIntent(orderId, true) }
        coVerify(exactly = 0) { paymentRepository.createPaymentIntent(any(), false) }
        val sheet = sheets.single()
        assertEquals("pi_secret", sheet.clientSecret)
        assertEquals("cus_1", sheet.customerId)
        assertEquals("ek_1", sheet.ephemeralKey)
        assertEquals("CZK", sheet.currencyCode)
    }

    @Test
    fun `a card confirm whose intent is refused opens no sheet and leaves the button live`() = runTest {
        val vm = cardConfirmReady()
        coEvery { paymentRepository.createPaymentIntent(orderId, any()) } returns
            ApiResult.Error(ApiError.Network("offline"))
        val sheets = sheetsOpened(vm)
        advanceUntilIdle()

        vm.confirmRecurring()
        advanceUntilIdle()

        assertTrue(sheets.isEmpty())
        assertEquals(ActionState.Idle, vm.confirmRecurringState.value)
    }

    // ── the credit a card confirm spends ──

    /**
     * ConfirmRecurringOrder takes the customer's credit in its card arm, before any sheet opens, and
     * Rewards and Profile show the balance from the shared cache. So the card confirm re-reads it at
     * once, as a cancel does, whether or not the sheet is then paid.
     */
    @Test
    fun `a card confirm re-reads the credit balance at once`() = runTest {
        val vm = cardConfirmReady()
        advanceUntilIdle()

        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 1) { loyaltyRepository.refresh() }
    }

    @Test
    fun `a cash confirm spends no credit and leaves the balance alone`() = runTest {
        coEvery { repository.getById(orderId) } returns
            ApiResult.Success(recurringCashOccurrence(needsConfirmation = true))
        coEvery { repository.confirmRecurring(orderId, null) } returns
            ApiResult.Success(ConfirmRecurringOrderResponse(orderId = orderId))
        val vm = viewModel()
        advanceUntilIdle()

        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 0) { loyaltyRepository.refresh() }
    }

    @Test
    fun `a refused card confirm leaves the credit balance alone`() = runTest {
        val vm = cardConfirmReady()
        coEvery { repository.confirmRecurring(orderId, null) } returns
            ApiResult.Error(ApiError.BadRequest("refused"))
        advanceUntilIdle()

        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 0) { loyaltyRepository.refresh() }
    }

    @Test
    fun `the save tick is offered, unticked, only while a card occurrence awaits its confirmation`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(recurringCardOccurrence())
        val card = viewModel()
        advanceUntilIdle()
        assertTrue(card.offersCardSaving.value)
        assertFalse(card.saveCard.value)

        coEvery { repository.getById(orderId) } returns
            ApiResult.Success(recurringCardOccurrence(needsConfirmation = false))
        val confirmed = viewModel()
        advanceUntilIdle()
        assertFalse(confirmed.offersCardSaving.value)

        coEvery { repository.getById(orderId) } returns
            ApiResult.Success(recurringCashOccurrence(needsConfirmation = true))
        val cash = viewModel()
        advanceUntilIdle()
        assertFalse(cash.offersCardSaving.value)
    }

    // ── the terms tick on a recurring confirm — the booking's rule ──

    private fun cashConfirmReady(): OrderDetailViewModel {
        coEvery { repository.getById(orderId) } returns
            ApiResult.Success(recurringCashOccurrence(needsConfirmation = true))
        coEvery { repository.confirmRecurring(orderId, any()) } returns
            ApiResult.Success(ConfirmRecurringOrderResponse(orderId = orderId))
        return viewModel()
    }

    @Test
    fun `an account holding both consents in force is not asked and its confirm asserts nothing`() = runTest {
        val vm = cashConfirmReady()
        advanceUntilIdle()

        assertFalse(vm.termsAsked.value)
        assertTrue(vm.canConfirmRecurring.value)

        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.confirmRecurring(orderId, null) }
    }

    /** One of the two is not both: a consent to an older Terms version is not on this list either. */
    @Test
    fun `an account missing a consent in force is asked, and the confirm waits for the tick`() = runTest {
        coEvery { consentClient.grantedTypes() } returns setOf(SignupConsentType.PrivacyPolicy)
        val vm = cashConfirmReady()
        advanceUntilIdle()

        assertTrue(vm.termsAsked.value)
        assertFalse(vm.canConfirmRecurring.value)

        vm.confirmRecurring()
        advanceUntilIdle()
        coVerify(exactly = 0) { repository.confirmRecurring(any(), any()) }

        vm.setTermsAccepted(true)
        advanceUntilIdle()
        assertTrue(vm.canConfirmRecurring.value)
        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.confirmRecurring(orderId, true) }
    }

    /** The safe direction is always "ask" — a consent that might not exist is asked for. */
    @Test
    fun `a failed consent read asks for the tick`() = runTest {
        coEvery { consentClient.grantedTypes() } returns null
        val vm = cashConfirmReady()
        advanceUntilIdle()

        assertTrue(vm.termsAsked.value)
        assertFalse(vm.canConfirmRecurring.value)
    }

    /**
     * The server judges the texts in force for the visit's own market, which the consents read cannot
     * see, so its refusal is the answer: the tick is shown at once, unticked, and a later re-read of the
     * order does not take it away again.
     */
    @Test
    fun `a confirm refused over the terms shows the tick at once and the next confirm asserts it`() = runTest {
        val vm = cashConfirmReady()
        coEvery { repository.confirmRecurring(orderId, null) } returns
            ApiResult.Error(ApiError.BadRequest("refused", errorKey = "consent.terms_not_accepted"))
        advanceUntilIdle()
        assertFalse(vm.termsAsked.value)

        vm.confirmRecurring()
        advanceUntilIdle()

        assertTrue(vm.termsAsked.value)
        assertFalse(vm.termsAccepted.value)
        assertFalse(vm.canConfirmRecurring.value)
        assertEquals(ActionState.Idle, vm.confirmRecurringState.value)

        vm.refresh()
        advanceUntilIdle()
        assertTrue(vm.termsAsked.value)

        vm.setTermsAccepted(true)
        vm.confirmRecurring()
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.confirmRecurring(orderId, true) }
    }

    @Test
    fun `a confirm refused for another reason does not ask for the tick`() = runTest {
        val vm = cashConfirmReady()
        coEvery { repository.confirmRecurring(orderId, null) } returns
            ApiResult.Error(ApiError.BadRequest("refused", errorKey = "order.not_found"))
        advanceUntilIdle()

        vm.confirmRecurring()
        advanceUntilIdle()

        assertFalse(vm.termsAsked.value)
        assertTrue(vm.canConfirmRecurring.value)
    }

    @Test
    fun `the consents are read once, and only for a visit awaiting its confirmation`() = runTest {
        coEvery { repository.getById(orderId) } returns ApiResult.Success(order(5))
        viewModel()
        advanceUntilIdle()
        coVerify(exactly = 0) { consentClient.grantedTypes() }

        val vm = cashConfirmReady()
        advanceUntilIdle()
        vm.refresh()
        advanceUntilIdle()

        coVerify(exactly = 1) { consentClient.grantedTypes() }
    }
}
