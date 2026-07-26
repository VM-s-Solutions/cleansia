package cz.cleansia.partner.core.notifications

import cz.cleansia.core.network.ApiError
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

class NotificationFeedRepositoryTest {

    private lateinit var api: NotificationFeedApi

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
    }

    @Before
    fun setUp() {
        api = mockk()
    }

    private fun newRepo() = NotificationFeedRepository(api, json)

    private fun jsonBody(raw: String) = raw.toResponseBody("application/json".toMediaType())

    @Test
    fun getPage_givenSuccess_returnsBody() = runTest {
        val dto = PagedNotificationsDto(
            pageNumber = 1,
            pageSize = 20,
            total = 1,
            data = listOf(
                UserNotificationDto(
                    id = "n-1",
                    eventKey = "order.new_available",
                    args = mapOf("count" to "3"),
                    createdOn = "2026-07-17T10:00:00+00:00",
                ),
            ),
        )
        coEvery { api.paged(offset = 0, limit = 20) } returns Response.success(dto)

        val result = newRepo().getPage(offset = 0)

        assertTrue(result.isSuccess)
        assertEquals(dto, result.getOrNull())
    }

    @Test
    fun getPage_whenApiThrows_isSilentNetworkError() = runTest {
        coEvery { api.paged(offset = 0, limit = 20) } throws java.io.IOException("boom")

        val result = newRepo().getPage(offset = 0)

        assertTrue(result.isError)
        assertTrue(result.errorOrNull() is ApiError.Network)
    }

    /**
     * The wire contract, not the Kotlin one: `GetPagedUserNotifications.Request`
     * extends `DataRangeRequest`, which binds `Offset`/`Limit`. Sending
     * `pageNumber`/`pageSize` bound nothing, so the server silently fell back to
     * offset 0 and every "load more" returned the same first rows — duplicate
     * `LazyColumn` keys, which throws. A mocked interface can only pin argument
     * values, so this one goes over a real socket to pin the parameter NAMES.
     */
    @Test
    fun getPage_secondPage_sendsOffsetNotPageNumber() = runTest {
        val server = MockWebServer()
        server.start()
        try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody("""{"pageNumber":1,"pageSize":20,"total":0,"data":[]}"""),
            )
            val wireApi = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                .build()
                .create(NotificationFeedApi::class.java)

            NotificationFeedRepository(wireApi, json).getPage(offset = 40)

            assertEquals("/api/Notification/Paged?Offset=40&Limit=20", server.takeRequest().path)
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun refreshUnreadCount_givenSuccess_updatesTheBadgeFlow() = runTest {
        coEvery { api.unreadCount() } returns Response.success(UnreadNotificationCountDto(count = 3))

        val repo = newRepo()
        val result = repo.refreshUnreadCount()

        assertTrue(result.isSuccess)
        assertEquals(3, repo.unreadCount.value)
    }

    @Test
    fun refreshUnreadCount_givenHttpError_keepsThePreviousBadge() = runTest {
        val repo = newRepo()
        coEvery { api.unreadCount() } returns Response.success(UnreadNotificationCountDto(count = 2))
        repo.refreshUnreadCount()
        coEvery { api.unreadCount() } returns Response.error(500, jsonBody("{}"))

        val result = repo.refreshUnreadCount()

        assertTrue(result.isError)
        assertEquals(2, repo.unreadCount.value)
    }

    @Test
    fun onPushReceived_incrementsTheBadgeLocally() = runTest {
        val repo = newRepo()

        repo.onPushReceived()
        repo.onPushReceived()

        assertEquals(2, repo.unreadCount.value)
    }

    @Test
    fun decrementUnread_floorsAtZero() = runTest {
        val repo = newRepo()
        repo.onPushReceived()

        repo.decrementUnread()
        repo.decrementUnread()

        assertEquals(0, repo.unreadCount.value)
    }

    @Test
    fun markAllRead_sendsTheWatermarkBody() = runTest {
        coEvery {
            api.markAllRead(MarkAllNotificationsReadRequest(upToCreatedOn = "2026-07-18T09:00:00+00:00"))
        } returns Response.success(MarkAllNotificationsReadResponse(markedCount = 2))

        val result = newRepo().markAllRead("2026-07-18T09:00:00+00:00")

        assertTrue(result.isSuccess)
    }

    @Test
    fun markRead_sendsTheRowId() = runTest {
        coEvery {
            api.markRead(MarkNotificationReadRequest(id = "n-1"))
        } returns Response.success(MarkNotificationReadResponse(id = "n-1", readOn = "2026-07-18T10:00:00+00:00"))

        val result = newRepo().markRead("n-1")

        assertTrue(result.isSuccess)
    }

    @Test
    fun clear_resetsTheBadge_sessionHygiene() = runTest {
        val repo = newRepo()
        repo.onPushReceived()

        repo.clear()

        assertEquals(0, repo.unreadCount.value)
    }
}
