package cz.cleansia.customer.core.orders

import android.content.Context
import android.content.res.Resources
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.WireContractViolation
import cz.cleansia.customer.R
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class GuestOrderRepositoryTest {
    private val api = mockk<GuestOrderApi>()
    private val resources = mockk<Resources> {
        every { getIdentifier("error_order_not_found", "string", "cz.cleansia.customer") } returns R.string.error_order_not_found
    }
    private val context = mockk<Context> {
        every { resources } returns this@GuestOrderRepositoryTest.resources
        every { packageName } returns "cz.cleansia.customer"
        every { getString(R.string.error_order_not_found) } returns "Booking not found"
    }
    private val repository = GuestOrderRepository(api, context)
    private val problem = """{"title":"One or more validation errors occurred.","status":400,"errors":{"DisplayOrderNumber":["order.not_found"]}}"""

    @Test
    fun `wrong key and account owned preview use the same localized problem response`() = runTest {
        coEvery { api.lookup(any(), any(), any()) } returns Response.error(
            400, problem.toResponseBody("application/problem+json".toMediaType()),
        )
        coEvery { api.preview(any(), any(), any()) } returns Response.error(
            400, problem.toResponseBody("application/problem+json".toMediaType()),
        )
        val lookup = repository.lookup("wrong", "guest@example.test", "secret") as ApiResult.Error
        val preview = repository.preview("CZ-123", "guest@example.test", "secret") as ApiResult.Error
        assertEquals("Booking not found", lookup.error.getUserMessage())
        assertEquals(lookup.error, preview.error)
    }

    @Test
    fun `malformed successful lookup stays a contract error instead of a network error`() = runTest {
        coEvery { api.lookup(any(), any(), any()) } throws WireContractViolation("totalPrice")
        val result = repository.lookup("CZ-123", "guest@example.test", "secret") as ApiResult.Error
        assertTrue(result.error is ApiError.Server)
        assertTrue((result.error as ApiError.Server).diagnostic!!.startsWith("totalPrice "))
    }

    @Test
    fun `cancel refusal remains an error instead of a successful receipt`() = runTest {
        coEvery { api.cancel(any(), any(), any(), any(), any()) } returns Response.error(
            400, problem.toResponseBody("application/problem+json".toMediaType()),
        )
        val result = repository.cancel("CZ-123", "guest@example.test", "secret", "schedule_changed", "en")
        assertTrue(result is ApiResult.Error)
    }
}
