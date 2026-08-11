package cz.cleansia.customer.core.booking

import cz.cleansia.customer.api.client.OrderApi
import io.mockk.coEvery
import io.mockk.mockk
import io.mockk.slot
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import retrofit2.Response
import cz.cleansia.customer.api.model.AppliedDiscountSource as GenAppliedDiscountSource
import cz.cleansia.customer.api.model.CreateOrderCommand as GenCreateOrderCommand
import cz.cleansia.customer.api.model.CreateOrderResponse as GenCreateOrderResponse
import cz.cleansia.customer.api.model.QuoteOrderResponse as GenQuoteOrderResponse

/**
 * Pins the last hop before the wire — the app→generated mapping in
 * [BookingApi.create]. Every field on the generated command defaults to `null`,
 * so a mapping this adapter drops still compiles and ships silently; the
 * ViewModel tests capture the app DTO one hop earlier and stay green through it.
 */
class BookingApiTest {

    private lateinit var orderApi: OrderApi

    @Before
    fun setUp() {
        orderApi = mockk()
    }

    private fun command(
        specialInstructions: String?,
        accessInstructions: String?,
    ) = CreateOrderCommand(
        customerName = "Ada Lovelace",
        customerEmail = "user@example.com",
        customerPhone = "+420600000000",
        selectedPackageIds = emptyList(),
        selectedServiceIds = listOf("s-1"),
        rooms = 2,
        bathrooms = 1,
        cleaningDate = "2026-08-01T09:00:00Z",
        paymentType = 1,
        totalPrice = 100.0,
        specialInstructions = specialInstructions,
        accessInstructions = accessInstructions,
    )

    @Test
    fun create_carriesBothInstructionNotesOntoTheGeneratedCommand() = runTest {
        val sent = slot<GenCreateOrderCommand>()
        coEvery { orderApi.orderCreateOrder(capture(sent)) } returns Response.success(
            GenCreateOrderResponse(id = "o-1", confirmationCode = "ABC123"),
        )

        BookingApi(orderApi).create(
            command(
                specialInstructions = "Gate code 1234, dog is friendly.",
                accessInstructions = "Side gate, key box code 4417.",
            ),
        )

        assertEquals("Gate code 1234, dog is friendly.", sent.captured.specialInstructions)
        assertEquals("Side gate, key box code 4417.", sent.captured.accessInstructions)
    }

    /**
     * The waiver verdict decides whether the summary charges the member 20 % or nothing, and a mapper
     * that drops it defaults to `false` — which silently re-charges every waived booking while the
     * whole suite stays green.
     */
    @Test
    fun quote_carriesTheServerWaiverVerdictOntoTheAppDto() = runTest {
        coEvery { orderApi.orderQuote(any()) } returns Response.success(
            GenQuoteOrderResponse(
                totalPrice = 1000.0,
                finalPriceAfterDiscount = 900.0,
                originalSubtotal = 1000.0,
                appliedDiscountSource = GenAppliedDiscountSource._2,
                currencyId = "c-1",
                currencyCode = "CZK",
                servicesSubtotal = 1000.0,
                packagesSubtotal = 0.0,
                extrasSubtotal = 0.0,
                expressSurchargeApplied = false,
                expressSurchargeAmount = 0.0,
                expressSurchargeWaivedByMembership = true,
                exchangeRate = 1.0,
            ),
        )

        val quoted = BookingApi(orderApi).quote(
            QuoteOrderCommand(
                selectedServiceIds = listOf("s-1"),
                selectedPackageIds = emptyList(),
                rooms = 2,
                bathrooms = 1,
                cleaningDate = "2026-08-05T13:00:00Z",
            ),
        ).body()

        assertEquals(true, quoted?.expressSurchargeWaivedByMembership)
    }
}
