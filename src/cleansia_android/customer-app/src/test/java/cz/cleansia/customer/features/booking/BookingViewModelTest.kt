package cz.cleansia.customer.features.booking
import cz.cleansia.core.auth.TokenStore

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.BookingApi
import cz.cleansia.customer.core.booking.CreateOrderCommand
import cz.cleansia.customer.core.booking.CreateOrderResponse
import cz.cleansia.customer.core.booking.QuoteOrderCommand
import cz.cleansia.customer.core.booking.QuoteOrderResponse
import cz.cleansia.customer.core.memberships.ExpressWaiverStatus
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.payments.CreatePaymentIntentResponse
import cz.cleansia.customer.core.payments.PaymentRepository
import cz.cleansia.customer.core.promo.PromoCodeApi
import cz.cleansia.customer.core.promo.PromoCodeError
import cz.cleansia.customer.core.promo.ValidatePromoCodeRequest
import cz.cleansia.customer.core.promo.ValidatePromoCodeResponse
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.referral.ValidateReferralResponse
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.slot
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.datetime.Clock
import kotlinx.datetime.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

/**
 * BookingViewModel tests — covers submit + quote + promo/referral validation.
 *
 * Uses [MainDispatcherRule] (StandardTestDispatcher) so `viewModelScope.launch`
 * inside the VM (the quote watcher + every suspend method) is driven by the
 * test scheduler. Tests call `advanceUntilIdle()` to drain queued coroutines
 * before asserting.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class BookingViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var bookingApi: BookingApi
    private lateinit var userRepository: UserRepository
    private lateinit var promoCodeApi: PromoCodeApi
    private lateinit var referralRepository: ReferralRepository
    private lateinit var paymentRepository: PaymentRepository
    private lateinit var tokenStore: cz.cleansia.core.auth.TokenStore
    private lateinit var snackbar: SnackbarController
    private lateinit var serviceAreaProvider: cz.cleansia.core.servicearea.ServiceAreaProvider
    private lateinit var membershipRepository: MembershipRepository
    private lateinit var appContext: Context

    private val currentUserFlow = MutableStateFlow<CurrentUser?>(null)
    private val membershipFlow = MutableStateFlow<GetMyMembershipResponse?>(null)

    private val networkMessage = "Check your internet connection and try again."
    private val pickTimeMessage = "Please select a cleaning date and time."
    private val signInMessage = "Please sign in to complete your booking."
    private val profileIncompleteMessage = "Please complete your profile."

    @Before
    fun setUp() {
        bookingApi = mockk()
        userRepository = mockk(relaxed = true)
        promoCodeApi = mockk()
        referralRepository = mockk(relaxed = true)
        paymentRepository = mockk(relaxed = true)
        tokenStore = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        serviceAreaProvider = mockk()
        coEvery { serviceAreaProvider.loadCountries() } returns emptyList()
        membershipRepository = mockk(relaxed = true)
        every { membershipRepository.current } returns membershipFlow
        appContext = mockk(relaxed = true)

        every { userRepository.currentUser } returns currentUserFlow
        coEvery { userRepository.refreshCurrentUser() } returns ApiResult.Success(Unit)
        // Default: signed in. Tests that exercise the signed-out branch
        // override this per-test.
        every { tokenStore.current() } returns mockk(relaxed = true)

        every { appContext.getString(R.string.error_generic_network) } returns networkMessage
        every { appContext.getString(R.string.error_booking_pick_time) } returns pickTimeMessage
        every { appContext.getString(R.string.error_booking_sign_in_required) } returns signInMessage
        every { appContext.getString(R.string.error_booking_profile_incomplete) } returns profileIncompleteMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns "unknown"
        every { appContext.getString(R.string.error_generic_server) } returns "server"
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "unauth"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun newViewModel(): BookingViewModel = BookingViewModel(
        bookingApi = bookingApi,
        userRepository = userRepository,
        promoCodeApi = promoCodeApi,
        referralRepository = referralRepository,
        paymentRepository = paymentRepository,
        tokenStore = tokenStore,
        snackbar = snackbar,
        serviceAreaProvider = serviceAreaProvider,
        membershipRepository = membershipRepository,
        appContext = appContext,
    )

    private fun completeUser() = CurrentUser(
        id = "u-1",
        email = "user@example.com",
        firstName = "Ada",
        lastName = "Lovelace",
        phoneNumber = "+420600000000",
        birthDate = null,
        preferredLanguageCode = "en",
    )

    private fun futureCleaningInstant(): Instant =
        Instant.fromEpochMilliseconds(Clock.System.now().toEpochMilliseconds() + 24L * 60 * 60 * 1000)

    // ── submit() ──

    @Test
    fun submit_givenCompleteUserAndCashFlow_returnsSuccessAndIdleState() = runTest {
        currentUserFlow.value = completeUser()
        val quote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "cur-1",
            currencyCode = "CZK",
            servicesSubtotal = 80.0,
            packagesSubtotal = 20.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        coEvery { bookingApi.create(any()) } returns Response.success(
            CreateOrderResponse(id = "o-1", confirmationCode = "ABC123"),
        )

        val vm = newViewModel()
        vm.update {
            it.copy(
                selectedServiceIds = setOf("s-1"),
                selectedInstant = futureCleaningInstant(),
                paymentMethod = "cash",
                street = "Wenceslas",
                city = "Prague",
                zipCode = "11000",
            )
        }
        advanceUntilIdle()

        val outcome = vm.submit()
        advanceUntilIdle()

        assertTrue("expected Success but was $outcome", outcome is BookingSubmitOutcome.Success)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    /**
     * The confirm step has always captured this text into [BookingState], but the
     * create command had nowhere to put it, so it never left the device. Pin the
     * wiring: what the user types must reach the wire.
     */
    @Test
    fun submit_givenSpecialInstructions_sendsThemOnTheCreateCommand() = runTest {
        currentUserFlow.value = completeUser()
        val quote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "cur-1",
            currencyCode = "CZK",
            servicesSubtotal = 100.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        val sent = slot<CreateOrderCommand>()
        coEvery { bookingApi.create(capture(sent)) } returns Response.success(
            CreateOrderResponse(id = "o-1", confirmationCode = "ABC123"),
        )

        val vm = newViewModel()
        vm.update {
            it.copy(
                selectedServiceIds = setOf("s-1"),
                selectedInstant = futureCleaningInstant(),
                paymentMethod = "cash",
                street = "Wenceslas",
                city = "Prague",
                zipCode = "11000",
                specialInstructions = "  Gate code 1234, dog is friendly.  ",
            )
        }
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        assertEquals("Gate code 1234, dog is friendly.", sent.captured.specialInstructions)
    }

    /** A user who tapped into the field and back out must not persist an empty note. */
    @Test
    fun submit_givenBlankSpecialInstructions_sendsNull() = runTest {
        currentUserFlow.value = completeUser()
        val quote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "cur-1",
            currencyCode = "CZK",
            servicesSubtotal = 100.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        val sent = slot<CreateOrderCommand>()
        coEvery { bookingApi.create(capture(sent)) } returns Response.success(
            CreateOrderResponse(id = "o-1", confirmationCode = "ABC123"),
        )

        val vm = newViewModel()
        vm.update {
            it.copy(
                selectedServiceIds = setOf("s-1"),
                selectedInstant = futureCleaningInstant(),
                paymentMethod = "cash",
                street = "Wenceslas",
                city = "Prague",
                zipCode = "11000",
                specialInstructions = "   \n ",
            )
        }
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        assertNull(sent.captured.specialInstructions)
    }

    /**
     * Entry instructions reach the cleaner, so a note the user typed must survive
     * the trip to the wire — trimmed, exactly like its special-instructions sibling.
     */
    @Test
    fun submit_givenAccessInstructions_sendsThemOnTheCreateCommand() = runTest {
        currentUserFlow.value = completeUser()
        val quote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "cur-1",
            currencyCode = "CZK",
            servicesSubtotal = 100.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        val sent = slot<CreateOrderCommand>()
        coEvery { bookingApi.create(capture(sent)) } returns Response.success(
            CreateOrderResponse(id = "o-1", confirmationCode = "ABC123"),
        )

        val vm = newViewModel()
        vm.update {
            it.copy(
                selectedServiceIds = setOf("s-1"),
                selectedInstant = futureCleaningInstant(),
                paymentMethod = "cash",
                street = "Wenceslas",
                city = "Prague",
                zipCode = "11000",
                accessInstructions = "  Side gate, key box code 4417.  ",
            )
        }
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        assertEquals("Side gate, key box code 4417.", sent.captured.accessInstructions)
    }

    /** Same blank-normalisation contract as special instructions — no empty note persisted. */
    @Test
    fun submit_givenBlankAccessInstructions_sendsNull() = runTest {
        currentUserFlow.value = completeUser()
        val quote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "cur-1",
            currencyCode = "CZK",
            servicesSubtotal = 100.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        val sent = slot<CreateOrderCommand>()
        coEvery { bookingApi.create(capture(sent)) } returns Response.success(
            CreateOrderResponse(id = "o-1", confirmationCode = "ABC123"),
        )

        val vm = newViewModel()
        vm.update {
            it.copy(
                selectedServiceIds = setOf("s-1"),
                selectedInstant = futureCleaningInstant(),
                paymentMethod = "cash",
                street = "Wenceslas",
                city = "Prague",
                zipCode = "11000",
                accessInstructions = "   \n ",
            )
        }
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        assertNull(sent.captured.accessInstructions)
    }

    /**
     * Backend rejects anything past 2000 UTF-16 code units. Capping on input
     * means the user hits the limit at the keystroke instead of at submit.
     */
    @Test
    fun updateAccessInstructions_whenLongerThanBackendLimit_capsAtMaxLength() = runTest {
        val vm = newViewModel()

        vm.updateAccessInstructions("k".repeat(BookingViewModel.ACCESS_INSTRUCTIONS_MAX_LENGTH + 500))

        assertEquals(
            BookingViewModel.ACCESS_INSTRUCTIONS_MAX_LENGTH,
            vm.state.value.accessInstructions.length,
        )
    }

    @Test
    fun updateAccessInstructions_whenWithinBackendLimit_keepsTextVerbatim() = runTest {
        val vm = newViewModel()

        vm.updateAccessInstructions("  Ring the bell twice.  ")

        assertEquals("  Ring the bell twice.  ", vm.state.value.accessInstructions)
    }

    @Test
    fun submit_whenAlreadySubmitting_secondCallShortCircuitsToFailed() = runTest {
        currentUserFlow.value = completeUser()
        // Pre-cache a quote so the watcher (which fires on state changes during
        // the 400ms debounce window) doesn't compete with submit() for the API.
        val cachedQuote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "c",
            currencyCode = "CZK",
            servicesSubtotal = 100.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(cachedQuote)

        // Hold submit() in flight by gating /Create instead.
        val gate = CompletableDeferred<Response<CreateOrderResponse>>()
        coEvery { bookingApi.create(any()) } coAnswers { gate.await() }

        val vm = newViewModel()
        vm.update {
            it.copy(
                selectedServiceIds = setOf("s-1"),
                selectedInstant = futureCleaningInstant(),
                paymentMethod = "cash",
            )
        }
        // Advance virtual time past the watcher debounce so the cached quote lands.
        advanceUntilIdle()
        assertTrue(vm.quoteState.value is QuoteState.Quoted)

        val first = async { vm.submit() }
        // Drive the test scheduler enough for the first submit() to flip into
        // Submitting and suspend on the gated /Create call. runCurrent runs all
        // currently-scheduled coroutines without advancing virtual time.
        runCurrent()
        assertEquals(ActionState.Submitting, vm.submitState.value)

        val secondOutcome = vm.submit()
        assertEquals(BookingSubmitOutcome.Failed, secondOutcome)

        // The re-entrant call must NOT have hit /Create. Only the first did.
        coVerify(exactly = 1) { bookingApi.create(any()) }

        gate.complete(Response.success(CreateOrderResponse(id = "o-1", confirmationCode = "X")))
        first.await()
        advanceUntilIdle()
    }

    @Test
    fun submit_whenNoToken_returnsFailedAndShowsSignInSnackbar() = runTest {
        // Genuine signed-out — no JWT in TokenStore. Even if a stale cached
        // user happened to be present, the token is the source of truth.
        every { tokenStore.current() } returns null
        currentUserFlow.value = null
        val vm = newViewModel()
        advanceUntilIdle()

        val outcome = vm.submit()
        advanceUntilIdle()

        assertEquals(BookingSubmitOutcome.Failed, outcome)
        verify { snackbar.showError(signInMessage) }
    }

    @Test
    fun submit_whenSignedInButProfileFetchFailed_showsNetworkErrorNotSignIn() = runTest {
        // Token present (signed in) but the in-memory profile cache is null
        // — happens when /User/GetCurrent failed earlier (e.g. coroutine
        // cancellation when the user fast-switched tabs on Home). The bug
        // before this fix was conflating these two states and surfacing
        // "Please sign in" to a user whose JWT was actually still valid.
        currentUserFlow.value = null
        val vm = newViewModel()
        advanceUntilIdle()

        val outcome = vm.submit()
        advanceUntilIdle()

        assertEquals(BookingSubmitOutcome.Failed, outcome)
        verify(exactly = 0) { snackbar.showError(signInMessage) }
        verify { snackbar.showError(networkMessage) }
    }

    @Test
    fun submit_whenProfileIncomplete_returnsProfileIncompleteAndShowsSnackbar() = runTest {
        currentUserFlow.value = completeUser().copy(phoneNumber = null)
        val vm = newViewModel()
        advanceUntilIdle()

        val outcome = vm.submit()
        advanceUntilIdle()

        assertEquals(BookingSubmitOutcome.ProfileIncomplete, outcome)
        verify { snackbar.showError(profileIncompleteMessage) }
    }

    @Test
    fun submit_whenNoCleaningInstant_returnsFailedAndShowsPickTimeSnackbar() = runTest {
        currentUserFlow.value = completeUser()
        val vm = newViewModel()
        advanceUntilIdle()

        val outcome = vm.submit()
        advanceUntilIdle()

        assertEquals(BookingSubmitOutcome.Failed, outcome)
        verify { snackbar.showError(pickTimeMessage) }
    }

    @Test
    fun submit_givenCardFlow_returnsCardPendingWithPaymentParams() = runTest {
        currentUserFlow.value = completeUser()
        val quote = QuoteOrderResponse(
            totalPrice = 50.0,
            currencyId = "c",
            currencyCode = "CZK",
            servicesSubtotal = 50.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        coEvery { bookingApi.create(any()) } returns Response.success(
            CreateOrderResponse(id = "o-1", confirmationCode = "X"),
        )
        coEvery { paymentRepository.createPaymentIntent("o-1") } returns ApiResult.Success(
            CreatePaymentIntentResponse(
                clientSecret = "pi_secret",
                paymentIntentId = "pi_1",
                stripeCustomerId = "cus_1",
                ephemeralKey = "ek",
            ),
        )

        val vm = newViewModel()
        vm.update {
            it.copy(
                selectedServiceIds = setOf("s-1"),
                selectedInstant = futureCleaningInstant(),
                paymentMethod = "card",
            )
        }
        advanceUntilIdle()

        val outcome = vm.submit()
        advanceUntilIdle()

        assertTrue("expected CardPending but was $outcome", outcome is BookingSubmitOutcome.CardPending)
        val cardPending = outcome as BookingSubmitOutcome.CardPending
        assertEquals("pi_secret", cardPending.paymentSheet.clientSecret)
        assertEquals("ek", cardPending.paymentSheet.ephemeralKey)
        assertEquals("cus_1", cardPending.paymentSheet.customerId)
    }

    // ── refreshQuote() (driven by the debounced watcher) ──

    @Test
    fun quoteWatcher_givenSuccessfulQuote_emitsQuotedState() = runTest {
        val quote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "c",
            currencyCode = "CZK",
            servicesSubtotal = 100.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        // Watcher debounces 400ms; runTest's TestDispatcher virtual-clocks the delay.
        advanceUntilIdle()

        val state = vm.quoteState.value
        assertTrue("expected Quoted but was $state", state is QuoteState.Quoted)
        assertEquals(quote, (state as QuoteState.Quoted).response)
    }

    @Test
    fun quoteWatcher_failureWithPriorCachedQuote_fallsBackToPrevious() = runTest {
        val firstQuote = QuoteOrderResponse(
            totalPrice = 100.0,
            currencyId = "c",
            currencyCode = "CZK",
            servicesSubtotal = 100.0,
            packagesSubtotal = 0.0,
            exchangeRate = 1.0,
        )

        coEvery { bookingApi.quote(any<QuoteOrderCommand>()) } returns Response.success(firstQuote)

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        advanceUntilIdle()
        assertTrue(vm.quoteState.value is QuoteState.Quoted)

        // Now make the next quote fail (different inputs trigger another watcher fire).
        coEvery { bookingApi.quote(any<QuoteOrderCommand>()) } throws java.io.IOException("boom")

        vm.update { it.copy(selectedServiceIds = setOf("s-1", "s-2")) }
        advanceUntilIdle()

        // Per QuoteState contract: on swallowed failure with previous Quoted,
        // fall back to that prior response rather than dropping to Idle.
        val state = vm.quoteState.value
        assertTrue("expected Quoted (fallback) but was $state", state is QuoteState.Quoted)
        assertEquals(firstQuote, (state as QuoteState.Quoted).response)
    }

    @Test
    fun quoteWatcher_givenEmptyInputs_returnsToIdle() = runTest {
        val vm = newViewModel()
        advanceUntilIdle()
        assertEquals(QuoteState.Idle, vm.quoteState.value)
    }

    // ── promo code validation ──

    @Test
    fun validatePromoCodeNow_givenValidCode_transitionsToValidAndPersistsCode() = runTest {
        coEvery { promoCodeApi.validate(any()) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 25.0),
        )

        val vm = newViewModel()
        val state = vm.validatePromoCodeNow("welcome20")

        assertTrue("expected Valid but was $state", state is PromoCodeUiState.Valid)
        assertEquals(25.0, (state as PromoCodeUiState.Valid).discountAmount, 0.0001)
        // Normalised + persisted into BookingState
        assertEquals("WELCOME20", vm.state.value.promoCode)
    }

    @Test
    fun validatePromoCodeNow_givenInvalidCode_transitionsToInvalidWithMappedError() = runTest {
        coEvery { promoCodeApi.validate(any()) } returns Response.success(
            ValidatePromoCodeResponse(isValid = false, errorCode = "Expired"),
        )

        val vm = newViewModel()
        val state = vm.validatePromoCodeNow("OLDCODE")

        assertTrue("expected Invalid but was $state", state is PromoCodeUiState.Invalid)
        assertEquals(PromoCodeError.Expired, (state as PromoCodeUiState.Invalid).error)
        // Invalid → BookingState.promoCode stays empty
        assertEquals("", vm.state.value.promoCode)
    }

    @Test
    fun validatePromoCodeNow_givenBlankCode_returnsIdle() = runTest {
        val vm = newViewModel()
        val state = vm.validatePromoCodeNow("   ")
        assertEquals(PromoCodeUiState.Idle, state)
    }

    @Test
    fun validatePromoCodeNow_whenApiThrows_transitionsToInvalidNullError() = runTest {
        coEvery { promoCodeApi.validate(any()) } throws java.io.IOException("boom")

        val vm = newViewModel()
        val state = vm.validatePromoCodeNow("CODE")

        assertTrue(state is PromoCodeUiState.Invalid)
        assertNull((state as PromoCodeUiState.Invalid).error)
    }

    /**
     * `CreateOrder.Handler` previews the promo against `calc.TotalPrice - calc.ExpressSurchargeAmount`
     * and applies the surcharge afterwards, so the gross is a base the submit never reproduces: a
     * percentage previews a fifth too much and a minimum-order floor clears that the submit refuses.
     */
    @Test
    fun validatePromoCodeNow_onAnExpressQuote_validatesAgainstThePreSurchargeSubtotal() = runTest {
        coEvery { bookingApi.quote(any()) } returns Response.success(
            quoteWith(totalPrice = 1200.0, surcharge = 200.0),
        )
        val request = slot<ValidatePromoCodeRequest>()
        coEvery { promoCodeApi.validate(capture(request)) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 100.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        advanceUntilIdle()
        vm.validatePromoCodeNow("CODE")

        assertEquals(1000.0, request.captured.orderSubtotal, 0.001)
    }

    /**
     * The validator answers on the pre-surcharge base while the price it comes off carries the
     * surcharge. Left as it arrives, the summary reads 1100 against a charged 1080.
     */
    @Test
    fun validatePromoCodeNow_onAnExpressQuote_restatesTheDiscountAgainstThePriceItComesOff() = runTest {
        val quote = quoteWith(totalPrice = 1200.0, surcharge = 200.0)
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        coEvery { promoCodeApi.validate(any()) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 100.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        advanceUntilIdle()
        val state = vm.validatePromoCodeNow("CODE")
        advanceUntilIdle()

        assertEquals(120.0, (state as PromoCodeUiState.Valid).discountAmount, 0.001)
        assertEquals(1080.0, BookingPriceSummary.resolve(quote, vm.effectiveDiscount.value).total, 0.001)
    }

    /**
     * No surcharge, no delta — the base is the whole quote and the answer is already stated against the
     * price it comes off. A fix that moves either number here has over-corrected.
     */
    @Test
    fun validatePromoCodeNow_withoutAnExpressSurcharge_sendsTheQuoteWholeAndLeavesTheAnswerAlone() = runTest {
        val quote = quoteWith(totalPrice = 1000.0, surcharge = 0.0, surchargeApplied = false)
        coEvery { bookingApi.quote(any()) } returns Response.success(quote)
        val request = slot<ValidatePromoCodeRequest>()
        coEvery { promoCodeApi.validate(capture(request)) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 100.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        advanceUntilIdle()
        val state = vm.validatePromoCodeNow("CODE")
        advanceUntilIdle()

        assertEquals(1000.0, request.captured.orderSubtotal, 0.001)
        assertEquals(100.0, (state as PromoCodeUiState.Valid).discountAmount, 0.001)
        assertEquals(900.0, BookingPriceSummary.resolve(quote, vm.effectiveDiscount.value).total, 0.001)
    }

    /** A membership waiver leaves an express hour charging no surcharge, so it is the plain case. */
    @Test
    fun validatePromoCodeNow_onAWaivedExpressQuote_sendsTheQuoteWholeAndLeavesTheAnswerAlone() = runTest {
        coEvery { bookingApi.quote(any()) } returns Response.success(
            quoteWith(totalPrice = 1000.0, surcharge = 0.0, surchargeApplied = true, waived = true),
        )
        val request = slot<ValidatePromoCodeRequest>()
        coEvery { promoCodeApi.validate(capture(request)) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 100.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        advanceUntilIdle()
        val state = vm.validatePromoCodeNow("CODE")

        assertEquals(1000.0, request.captured.orderSubtotal, 0.001)
        assertEquals(100.0, (state as PromoCodeUiState.Valid).discountAmount, 0.001)
    }

    @Test
    fun clearPromoCode_resetsStateAndBookingPayload() = runTest {
        coEvery { promoCodeApi.validate(any()) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 10.0),
        )

        val vm = newViewModel()
        vm.validatePromoCodeNow("CODE")
        assertEquals("CODE", vm.state.value.promoCode)

        vm.clearPromoCode()
        assertEquals(PromoCodeUiState.Idle, vm.promoCodeState.value)
        assertEquals("", vm.state.value.promoCode)
    }

    // ── referral code validation ──

    @Test
    fun validateReferralCodeNow_givenValidCode_transitionsToValidAndPersistsCode() = runTest {
        coEvery { referralRepository.validate("FRIEND10") } returns ApiResult.Success(
            ValidateReferralResponse(
                isValid = true,
                referrerFirstName = "Bob",
            ),
        )

        val vm = newViewModel()
        val state = vm.validateReferralCodeNow("friend10")

        assertTrue("expected Valid but was $state", state is cz.cleansia.customer.features.booking.ReferralCodeUiState.Valid)
        assertEquals("FRIEND10", vm.state.value.referralCode)
    }

    @Test
    fun validateReferralCodeNow_givenInvalidCode_transitionsToInvalidWithMappedError() = runTest {
        coEvery { referralRepository.validate(any()) } returns ApiResult.Success(
            ValidateReferralResponse(
                isValid = false,
                errorCode = "SelfReferral",
            ),
        )

        val vm = newViewModel()
        val state = vm.validateReferralCodeNow("OWNCODE")

        assertTrue(state is cz.cleansia.customer.features.booking.ReferralCodeUiState.Invalid)
        assertEquals(
            cz.cleansia.customer.core.referral.ReferralValidationError.SelfReferral,
            (state as cz.cleansia.customer.features.booking.ReferralCodeUiState.Invalid).error,
        )
        assertEquals("", vm.state.value.referralCode)
    }

    // ── reset() ──

    @Test
    fun reset_clearsAllStateAndIndicators() = runTest {
        coEvery { promoCodeApi.validate(any()) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 5.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1"), street = "X") }
        vm.validatePromoCodeNow("CODE")
        advanceUntilIdle()

        vm.reset()

        assertEquals(BookingState(), vm.state.value)
        assertEquals(QuoteState.Idle, vm.quoteState.value)
        assertEquals(ActionState.Idle, vm.submitState.value)
        assertEquals(PromoCodeUiState.Idle, vm.promoCodeState.value)
    }

    // ── express waiver ──

    @Test
    fun expressWaiver_givenSignedInUser_warmsTheMembershipRead() = runTest {
        newViewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { membershipRepository.refresh() }
    }

    @Test
    fun expressWaiver_givenGuest_neverCallsTheMembershipEndpoint() = runTest {
        every { tokenStore.current() } returns null

        newViewModel()
        advanceUntilIdle()

        coVerify(exactly = 0) { membershipRepository.refresh() }
    }

    @Test
    fun expressWaiver_givenQuotaLeft_reportsTheServerCount() = runTest {
        membershipFlow.value = GetMyMembershipResponse(
            hasMembership = true,
            expressUpgradesPerMonth = 2,
            expressUpgradesRemaining = 2,
        )

        val vm = newViewModel()
        advanceUntilIdle()

        assertEquals(ExpressWaiverStatus.Available, vm.expressWaiver.value.status)
        assertEquals(2, vm.expressWaiver.value.remaining)
    }

    /**
     * A membership read that never lands must leave the wizard silent, not render a claim from
     * nothing on the most valuable screen in the product.
     */
    @Test
    fun expressWaiver_givenNoMembershipYet_staysSilent() = runTest {
        val vm = newViewModel()
        advanceUntilIdle()

        assertEquals(ExpressWaiverStatus.None, vm.expressWaiver.value.status)
    }

    // ── effectiveDiscount ──
    //
    // The sticky price bar used to redo this with the server discounts left out, so a Plus member on
    // the confirm step read one total on the receipt and a different one on the button.

    @Test
    fun effectiveDiscount_addsTierAndMembership() = runTest {
        coEvery { bookingApi.quote(any()) } returns Response.success(
            quoteWith(tierDiscount = 30.0, membershipDiscount = 20.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        advanceUntilIdle()

        assertEquals(50.0, vm.effectiveDiscount.value, 0.001)
    }

    @Test
    fun effectiveDiscount_givenLargerPromo_replacesTheServerPairRatherThanStacking() = runTest {
        coEvery { bookingApi.quote(any()) } returns Response.success(
            quoteWith(tierDiscount = 30.0, membershipDiscount = 20.0),
        )
        coEvery { promoCodeApi.validate(any()) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 120.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        vm.validatePromoCodeNow("CODE")
        advanceUntilIdle()

        assertEquals(120.0, vm.effectiveDiscount.value, 0.001)
    }

    @Test
    fun effectiveDiscount_givenSmallerPromo_keepsTheServerPair() = runTest {
        coEvery { bookingApi.quote(any()) } returns Response.success(
            quoteWith(tierDiscount = 30.0, membershipDiscount = 20.0),
        )
        coEvery { promoCodeApi.validate(any()) } returns Response.success(
            ValidatePromoCodeResponse(isValid = true, discountAmount = 10.0),
        )

        val vm = newViewModel()
        vm.update { it.copy(selectedServiceIds = setOf("s-1")) }
        vm.validatePromoCodeNow("CODE")
        advanceUntilIdle()

        assertEquals(50.0, vm.effectiveDiscount.value, 0.001)
    }

    @Test
    fun effectiveDiscount_withoutAQuote_isZero() = runTest {
        val vm = newViewModel()
        advanceUntilIdle()

        assertEquals(0.0, vm.effectiveDiscount.value, 0.001)
    }

    private fun quoteWith(
        tierDiscount: Double = 0.0,
        membershipDiscount: Double = 0.0,
        totalPrice: Double = 1000.0,
        surcharge: Double = 0.0,
        surchargeApplied: Boolean = surcharge > 0.0,
        waived: Boolean = false,
    ) = QuoteOrderResponse(
        totalPrice = totalPrice,
        tierDiscountAmount = tierDiscount,
        membershipDiscountAmount = membershipDiscount,
        currencyId = "cur-1",
        currencyCode = "CZK",
        servicesSubtotal = totalPrice,
        packagesSubtotal = 0.0,
        expressSurchargeApplied = surchargeApplied,
        expressSurchargeAmount = surcharge,
        expressSurchargeWaivedByMembership = waived,
        exchangeRate = 1.0,
    )
}
