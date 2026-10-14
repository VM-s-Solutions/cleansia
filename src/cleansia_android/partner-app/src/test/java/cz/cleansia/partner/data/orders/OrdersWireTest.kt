package cz.cleansia.partner.data.orders

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
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/** The Orders tabs' sort choice reaches the paged list over a socket, in the names the server binds. */
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
}
