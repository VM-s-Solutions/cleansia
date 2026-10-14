package cz.cleansia.partner.data.orders

import cz.cleansia.core.location.LocationService
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.features.orders.AvailableSort
import cz.cleansia.partner.features.orders.OrdersListViewModel
import cz.cleansia.partner.features.orders.OrdersTab
import cz.cleansia.partner.testing.MainDispatcherRule
import cz.cleansia.partner.testing.assertSortedBy
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.withTimeout
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.concurrent.TimeUnit

@OptIn(ExperimentalCoroutinesApi::class)
class AvailableOrdersWireTest {
    @get:Rule val mainRule = MainDispatcherRule(UnconfinedTestDispatcher())
    private val json = Json { ignoreUnknownKeys = true; explicitNulls = false }
    private val translator = mockk<ApiErrorTranslator>().also { every { it.translate(any()) } returns "translated error" }
    private val snackbar = mockk<SnackbarController>(relaxed = true)
    private val location = mockk<LocationService>().also {
        every { it.hasPermission() } returns false
        coEvery { it.getCurrentLocation() } returns null
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
        val repository = OrdersRepositoryImpl(retrofit.create(OrderApi::class.java), json, mockk(relaxed = true))
        return OrdersListViewModel(repository, resolver, translator, snackbar, location)
    }

    private fun page() = MockResponse().setHeader("Content-Type", "application/json")
        .setBody("""{"pageNumber":1,"pageSize":50,"total":0,"data":[]}""")

    @Test fun availableUsesCurrentEmployeeExclusionThroughRealResolverRepositoryAndGeneratedClient() = runTest {
        val server = MockWebServer()
        server.start()
        try {
            server.enqueue(MockResponse().setHeader("Content-Type", "application/json").setBody("""{"id":"resolved-current"}"""))
            server.enqueue(page())
            val vm = viewModel(server, storedId = null)
            withContext(Dispatchers.Default) { withTimeout(5_000) { vm.uiState.first { it.hasLoadedOnce } } }
            assertEquals("/api/Employee/GetCurrentEmployee", server.takeRequest(5, TimeUnit.SECONDS)?.requestUrl?.encodedPath)
            val request = server.takeRequest(5, TimeUnit.SECONDS)!!
            assertEquals("/api/Order/GetPaged", request.requestUrl?.encodedPath)
            val url = request.requestUrl!!
            assertEquals("true", url.queryParameter("Filter.HasAvailableSpots"))
            assertEquals("resolved-current", url.queryParameter("Filter.ExcludeEmployeeId"))
            assertNull(url.queryParameter("Filter.IsUnassigned"))
            assertNull(url.queryParameter("Filter.EmployeeId"))
            assertEquals(listOf("0", "2", "3", "4"), url.queryParameterValues("Filter.OrderStatuses"))
            assertSortedBy(request, "estimatedCleanerPay", 1)
            assertEquals("0", url.queryParameter("Offset"))
            assertEquals("50", url.queryParameter("Limit"))
            server.enqueue(page())
            vm.setAvailableSort(AvailableSort.SoonestFirst)
            withContext(Dispatchers.Default) { withTimeout(5_000) { vm.uiState.first { !it.isBackgroundRefreshing } } }
            val sorted = server.takeRequest(5, TimeUnit.SECONDS)!!
            assertSortedBy(sorted, "cleaningDateTime", 0)
            assertEquals("resolved-current", sorted.requestUrl?.queryParameter("Filter.ExcludeEmployeeId"))
        } finally { server.shutdown() }
    }

    @Test fun activeAndHistoryKeepPositiveEmployeeScopeWithoutAvailabilityFilters() = runTest {
        val server = MockWebServer()
        server.start()
        try {
            server.enqueue(page())
            val vm = viewModel(server)
            withContext(Dispatchers.Default) { withTimeout(5_000) { vm.uiState.first { it.hasLoadedOnce } } }
            server.takeRequest(5, TimeUnit.SECONDS)
            for (tab in listOf(OrdersTab.MyActive, OrdersTab.MyCompleted)) {
                server.enqueue(page())
                vm.selectTab(tab)
                withContext(Dispatchers.Default) { withTimeout(5_000) { vm.uiState.first { !it.isBackgroundRefreshing } } }
                val request = server.takeRequest(5, TimeUnit.SECONDS)!!
                val url = request.requestUrl!!
                assertEquals("current-employee", url.queryParameter("Filter.EmployeeId"))
                assertNull(url.queryParameter("Filter.HasAvailableSpots"))
                assertNull(url.queryParameter("Filter.ExcludeEmployeeId"))
                assertNull(url.queryParameter("Filter.IsUnassigned"))
                assertEquals(if (tab == OrdersTab.MyActive) listOf("2", "3", "4") else listOf("5"), url.queryParameterValues("Filter.OrderStatuses"))
                assertSortedBy(request, "cleaningDateTime", if (tab == OrdersTab.MyActive) 0 else 1)
                assertEquals("0", url.queryParameter("Offset"))
                assertEquals("50", url.queryParameter("Limit"))
            }
        } finally { server.shutdown() }
    }

    private suspend fun assertInvalidIdentity(body: String?, signedIn: Boolean = true) {
        val server = MockWebServer()
        server.start()
        try {
            if (signedIn) server.enqueue(MockResponse().setHeader("Content-Type", "application/json").setBody(body!!))
            server.enqueue(page())
            val vm = viewModel(server, storedId = " ", signedIn = signedIn)
            withContext(Dispatchers.Default) { withTimeout(5_000) { vm.uiState.first { it.hasLoadedOnce } } }
            if (signedIn) assertEquals("/api/Employee/GetCurrentEmployee", server.takeRequest(5, TimeUnit.SECONDS)?.requestUrl?.encodedPath)
            assertNull("invalid current identity must not issue an orders query", server.takeRequest(150, TimeUnit.MILLISECONDS))
            assertFalse(vm.uiState.value.isInitialLoad)
            assertFalse(vm.uiState.value.isBackgroundRefreshing)
            assertFalse(vm.uiState.value.isUserRefreshing)
            verify(exactly = 1) { translator.translate(ApiError.Unauthorized) }
            coVerify(exactly = 1) { snackbar.showError("translated error") }
        } finally { server.shutdown() }
    }

    @Test fun missingCurrentIdentityRefusesNetworkQuery() = runTest { assertInvalidIdentity("{}") }
    @Test fun nullCurrentIdentityRefusesNetworkQuery() = runTest { assertInvalidIdentity("""{"id":null}""") }
    @Test fun blankCurrentIdentityRefusesNetworkQuery() = runTest { assertInvalidIdentity("""{"id":"  "}""") }
    @Test fun missingSessionRefusesNetworkQuery() = runTest { assertInvalidIdentity(null, signedIn = false) }
}
