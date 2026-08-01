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
import cz.cleansia.customer.api.model.CreateOrderCommand as GenCreateOrderCommand
import cz.cleansia.customer.api.model.CreateOrderResponse as GenCreateOrderResponse

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
}
