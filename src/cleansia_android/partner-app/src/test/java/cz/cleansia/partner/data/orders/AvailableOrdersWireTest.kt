package cz.cleansia.partner.data.orders

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.payroll.PeriodPayRepository
import cz.cleansia.partner.features.orders.OrdersListUiState
import cz.cleansia.partner.features.orders.OrdersListViewModel
import cz.cleansia.partner.features.orders.OrdersTab
import cz.cleansia.partner.testing.MainDispatcherRule
import cz.cleansia.partner.testing.assertSortedBy
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.Collections

/**
 * The board's requests through the real resolver, repository and generated client: Available scoped to seats
 * the cleaner is not on, Active and History scoped to the cleaner, 100 per page, sorted as the server binds —
 * and no list request at all when the cleaner's own id cannot be read. Entry fires its reads concurrently, so
 * the server answers by path and the test reads what was sent.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class AvailableOrdersWireTest {
    @get:Rule val mainRule = MainDispatcherRule(UnconfinedTestDispatcher())
    private val json = Json { ignoreUnknownKeys = true; explicitNulls = false }
    private val translator = mockk<ApiErrorTranslator>().also { every { it.translate(any()) } returns "translated error" }
    private val snackbar = mockk<SnackbarController>(relaxed = true)
    private val periodPay = mockk<PeriodPayRepository>().also { coEvery { it.getCashHeld() } returns ApiResult.Success(emptyList()) }
    private val recorded: MutableList<RecordedRequest> = Collections.synchronizedList(mutableListOf())
    private lateinit var repository: OrdersRepositoryImpl

    private fun server(employeeBody: String?) = MockWebServer().apply {
        dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse {
                recorded += request
                val body = when (request.requestUrl?.encodedPath) {
                    "/api/Employee/GetCurrentEmployee" -> employeeBody ?: return MockResponse().setResponseCode(401)
                    "/api/Order/GetPaged" -> """{"pageNumber":1,"pageSize":100,"total":0,"data":[]}"""
                    "/api/Order/MyPendingOffers" -> "[]"
                    else -> return MockResponse().setResponseCode(404)
                }
                return MockResponse().setHeader("Content-Type", "application/json").setBody(body)
            }
        }
        start()
    }

    private fun viewModel(server: MockWebServer, storedId: String? = "current-employee", signedIn: Boolean = true): OrdersListViewModel {
        val retrofit = Retrofit.Builder().baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType())).build()
        var profile: UserProfileData? = if (signedIn) UserProfileData(
            "current-user", "cleaner@example.test", storedId, true, false, null, null, "Employee",
        ) else null
        val store = mockk<UserProfileStore>()
        coEvery { store.current() } answers { profile }
        coEvery { store.updateEmployeeId(any()) } answers { profile = profile?.copy(employeeId = firstArg()) }
        val resolver = EmployeeIdResolver(store, retrofit.create(EmployeeApi::class.java), json)
        repository = OrdersRepositoryImpl(retrofit.create(OrderApi::class.java), json, mockk(relaxed = true))
        return OrdersListViewModel(repository, resolver, translator, snackbar, periodPay)
    }

    private suspend fun settled(vm: OrdersListViewModel, tab: OrdersTab) = withContext(Dispatchers.Default) {
        withTimeout(5_000) {
            vm.uiState.first { state: OrdersListUiState -> state.pane(tab).let { (it.hasLoaded || it.failed) && !it.isLoading } }
        }
    }

    /**
     * Entry also refreshes the pending offers over the real client; let that read land before the test ends,
     * or its continuation resumes on a Main dispatcher the rule has already reset (and fails the next test).
     */
    private suspend fun drain() = withContext(Dispatchers.Default) {
        withTimeout(5_000) { while (repository.arePendingOffersStale()) delay(10) }
        delay(100)
    }

    private fun pagedRequests() = synchronized(recorded) { recorded.filter { it.requestUrl?.encodedPath == "/api/Order/GetPaged" } }

    @Test fun availableUsesCurrentEmployeeExclusionThroughRealResolverRepositoryAndGeneratedClient() = runTest {
        val server = server("""{"id":"resolved-current"}""")
        try {
            val vm = viewModel(server, storedId = null)
            settled(vm, OrdersTab.Available)
            val request = pagedRequests().single { it.requestUrl!!.queryParameter("Filter.HasAvailableSpots") != null }
            val url = request.requestUrl!!
            assertEquals("true", url.queryParameter("Filter.HasAvailableSpots"))
            assertEquals("resolved-current", url.queryParameter("Filter.ExcludeEmployeeId"))
            assertNull(url.queryParameter("Filter.IsUnassigned"))
            assertNull(url.queryParameter("Filter.EmployeeId"))
            assertNull(url.queryParameter("Filter.Id"))
            assertEquals(listOf("0", "2", "3", "4"), url.queryParameterValues("Filter.OrderStatuses"))
            assertSortedBy(request, "cleaningDateTime", 0)
            assertEquals("0", url.queryParameter("Offset"))
            assertEquals("100", url.queryParameter("Limit"))
            drain()
        } finally { server.shutdown() }
    }

    @Test fun activeAndHistoryKeepPositiveEmployeeScopeWithoutAvailabilityFilters() = runTest {
        val server = server("""{"id":"current-employee"}""")
        try {
            val vm = viewModel(server)
            settled(vm, OrdersTab.MyActive)
            vm.selectTab(OrdersTab.MyCompleted)
            settled(vm, OrdersTab.MyCompleted)
            for (tab in listOf(OrdersTab.MyActive, OrdersTab.MyCompleted)) {
                val statuses = if (tab == OrdersTab.MyActive) listOf("2", "3", "4") else listOf("5")
                // An empty month also reads last month as its reference: every History request keeps the same scope.
                val requests = pagedRequests().filter { it.requestUrl!!.queryParameterValues("Filter.OrderStatuses") == statuses }
                assertTrue(requests.isNotEmpty())
                for (request in requests) {
                    val url = request.requestUrl!!
                    assertEquals("current-employee", url.queryParameter("Filter.EmployeeId"))
                    assertNull(url.queryParameter("Filter.HasAvailableSpots"))
                    assertNull(url.queryParameter("Filter.ExcludeEmployeeId"))
                    assertNull(url.queryParameter("Filter.IsUnassigned"))
                    assertSortedBy(request, "cleaningDateTime", if (tab == OrdersTab.MyActive) 0 else 1)
                    assertEquals("0", url.queryParameter("Offset"))
                    assertEquals("100", url.queryParameter("Limit"))
                }
            }
            drain()
        } finally { server.shutdown() }
    }

    private suspend fun assertInvalidIdentity(body: String?, signedIn: Boolean = true) {
        val server = server(body)
        try {
            val vm = viewModel(server, storedId = " ", signedIn = signedIn)
            settled(vm, OrdersTab.Available)
            settled(vm, OrdersTab.MyActive)
            assertTrue("invalid current identity must not issue an orders query", pagedRequests().isEmpty())
            assertTrue(vm.uiState.value.pane(OrdersTab.Available).failed)
            assertTrue(vm.uiState.value.pane(OrdersTab.MyActive).failed)
            verify(exactly = 0) { snackbar.showError(any<String>()) }
            drain()
        } finally { server.shutdown() }
    }

    @Test fun missingCurrentIdentityRefusesNetworkQuery() = runTest { assertInvalidIdentity("{}") }
    @Test fun nullCurrentIdentityRefusesNetworkQuery() = runTest { assertInvalidIdentity("""{"id":null}""") }
    @Test fun blankCurrentIdentityRefusesNetworkQuery() = runTest { assertInvalidIdentity("""{"id":"  "}""") }
    @Test fun missingSessionRefusesNetworkQuery() = runTest { assertInvalidIdentity(null, signedIn = false) }
}
