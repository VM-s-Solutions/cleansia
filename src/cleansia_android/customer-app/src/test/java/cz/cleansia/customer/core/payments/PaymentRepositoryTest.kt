package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.ApiResult
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * PaymentRepository characterization + post-migration contract.
 *
 * Pins the observable behaviour of createPaymentIntent across the ApiResult
 * migration:
 *  - a successful body is returned verbatim ([ApiResult.Success]);
 *  - an HTTP error or a missing/malformed body yields a failure
 *    ([ApiResult.Error]) - the booking flow surfaces the message.
 *
 * The repo never surfaces a snackbar (it has no SnackbarController). The
 * user-facing message for a failed PaymentIntent stays in BookingViewModel.
 */
class PaymentRepositoryTest {

    private lateinit var api: PaymentApi

    private val json = Json { ignoreUnknownKeys = true }

    @Before
    fun setUp() {
        api = mockk()
    }

    private fun newRepo() = PaymentRepository(api, json)

    private fun intentResponse() = CreatePaymentIntentResponse(
        clientSecret = "pi_secret",
        paymentIntentId = "pi_1",
        stripeCustomerId = "cus_1",
        ephemeralKey = "ek",
    )

    @Test
    fun createPaymentIntent_givenSuccess_returnsBody() = runTest {
        val body = intentResponse()
        coEvery { api.createPaymentIntent(CreatePaymentIntentRequest("o-1")) } returns Response.success(body)

        val result = newRepo().createPaymentIntent("o-1")

        assertTrue(result.isSuccess)
        assertEquals(body, result.getOrNull())
    }

    @Test
    fun createPaymentIntent_givenHttpError_yieldsFailure() = runTest {
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { api.createPaymentIntent(any()) } returns Response.error(400, errBody)

        val result = newRepo().createPaymentIntent("o-1")

        assertTrue(result.isError)
    }

    @Test
    fun createPaymentIntent_whenApiThrows_yieldsFailure() = runTest {
        coEvery { api.createPaymentIntent(any()) } throws java.io.IOException("boom")

        val result = newRepo().createPaymentIntent("o-1")

        assertTrue(result.isError)
    }
}
