package cz.cleansia.customer.core.orders

import cz.cleansia.core.network.WireContractViolation
import io.mockk.coEvery
import io.mockk.mockk
import io.mockk.slot
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response
import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.CancellationFeeTier as GenCancellationFeeTier
import cz.cleansia.customer.api.model.GetCancellationFeePreviewResponse as GenPreviewResponse

/**
 * The last hop before the wire for the cancellation preview. Every field on the
 * generated response is nullable with a `= null` default, so a mapper line left
 * out compiles, ships, and reads as a zero fee — the class of miss that the
 * ViewModel tests cannot see because they mock this adapter away.
 */
class OrderApiTest {

    private lateinit var generated: GenOrderApi

    @Before
    fun setUp() {
        generated = mockk()
    }

    private fun response(
        tier: GenCancellationFeeTier? = GenCancellationFeeTier._3,
        expressWaiverForfeitedOnCancel: Boolean? = true,
    ) = GenPreviewResponse(
        orderId = "order-1",
        tier = tier,
        feeRate = 0.25,
        feeAmount = 250.0,
        refundAmount = 750.0,
        totalPrice = 1000.0,
        currencyCode = "CZK",
        expressWaiverForfeitedOnCancel = expressWaiverForfeitedOnCancel,
    )

    @Test
    fun `the preview carries every field the sheet renders`() = runTest {
        coEvery { generated.orderCancellationPreview(orderId = any()) } returns
            Response.success(response())

        val body = OrderApi(generated).getCancellationPreview("order-1").body()!!

        assertEquals("order-1", body.orderId)
        assertEquals(3, body.tier)
        assertEquals(0.25, body.feeRate, 0.0)
        assertEquals(250.0, body.feeAmount, 0.0)
        assertEquals(750.0, body.refundAmount, 0.0)
        assertEquals(1000.0, body.totalPrice, 0.0)
        assertEquals("CZK", body.currencyCode)
        assertTrue(body.expressWaiverForfeitedOnCancel)
    }

    @Test
    fun `the order id reaches the query parameter`() = runTest {
        val sent = slot<String>()
        coEvery { generated.orderCancellationPreview(orderId = capture(sent)) } returns
            Response.success(response())

        OrderApi(generated).getCancellationPreview("order-42")

        assertEquals("order-42", sent.captured)
    }

    @Test
    fun `a response without a tier maps to nothing rather than to a free cancellation`() = runTest {
        coEvery { generated.orderCancellationPreview(orderId = any()) } returns
            Response.success(response(tier = null))

        // Tier 0 is FreeNotAccepted, so any default here would quote a customer
        // a free cancellation on the strength of a field the server never sent.
        val violation = assertThrows(WireContractViolation::class.java) {
            runBlocking { OrderApi(generated).getCancellationPreview("order-1") }
        }
        assertTrue("expected the field name in \"${violation.message}\"", violation.message!!.startsWith("tier "))
    }

    @Test
    fun `every generated tier value survives the mapping`() = runTest {
        GenCancellationFeeTier.entries.forEach { tier ->
            coEvery { generated.orderCancellationPreview(orderId = any()) } returns
                Response.success(response(tier = tier))

            assertEquals(
                tier.value,
                OrderApi(generated).getCancellationPreview("order-1").body()!!.tier,
            )
        }
    }
}
