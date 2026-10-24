package cz.cleansia.partner.data.orders

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.testing.assertSortedBy
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * The Orders tabs' sort choice reaches the paged list over a socket, in the names the server binds, and a drop
 * reads the crew it leaves off the wire as the server wrote it.
 */
class OrdersWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = OrdersRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(OrderApi::class.java),
        json,
        mockk(relaxed = true),
    )

    private suspend fun pagedRequest(sortField: String, sortAscending: Boolean): RecordedRequest {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody("""{"pageNumber":1,"pageSize":50,"total":0,"data":[]}"""),
            )
            repo(server).getPaged(sortField = sortField, sortAscending = sortAscending)
            server.takeRequest()
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun anAscendingChoiceIsSentAsItsFieldAtDirectionZero() = runTest {
        val request = pagedRequest(sortField = "cleaningDateTime", sortAscending = true)

        assertEquals("/api/Order/GetPaged", request.requestUrl?.encodedPath)
        assertSortedBy(request, field = "cleaningDateTime", direction = 0)
    }

    @Test
    fun aDescendingChoiceIsSentAsItsFieldAtDirectionOne() = runTest {
        val request = pagedRequest(sortField = "estimatedCleanerPay", sortAscending = false)

        assertEquals("/api/Order/GetPaged", request.requestUrl?.encodedPath)
        assertSortedBy(request, field = "estimatedCleanerPay", direction = 1)
    }

    private suspend fun drop(answer: String): ApiResult<Int> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(answer),
            )
            val result = repo(server).dropOrder("order-1")
            val request = server.takeRequest()
            assertEquals("/api/Order/DropOrder", request.requestUrl?.encodedPath)
            assertEquals("""{"orderId":"order-1"}""", request.body.readUtf8())
            result
        } finally {
            server.shutdown()
        }
    }

    /** Zero is the answer the toast turns on ("it has no cleaner now"), so it must be read, not defaulted. */
    @Test
    fun aDropPostsTheOrderAndReadsTheCrewItLeaves() = runTest {
        assertEquals(ApiResult.Success(0), drop("""{"orderId":"order-1","crewRemaining":0}"""))
        assertEquals(ApiResult.Success(2), drop("""{"orderId":"order-1","crewRemaining":2}"""))
    }

    @Test
    fun aDropAnswerWithNoCrewCountIsRefusedRatherThanReadAsNoCrew() = runTest {
        val result = drop("""{"orderId":"order-1"}""")

        assertTrue("got $result", (result as? ApiResult.Error)?.error is ApiError.Server)
    }
}
