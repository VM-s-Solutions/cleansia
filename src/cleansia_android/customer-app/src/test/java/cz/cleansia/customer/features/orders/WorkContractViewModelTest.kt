package cz.cleansia.customer.features.orders

import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.orders.WorkContractAcceptanceDetailsDto
import cz.cleansia.customer.core.orders.WorkContractDto
import cz.cleansia.customer.core.orders.WorkContractFactsDto
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The contract screen is a fetch-and-render of one acceptance in the customer's UI language: the
 * funnel is Loading → Loaded/Error, a refused read is the error state with nothing of the order on
 * it, and a retry re-asks in the language current at that moment.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class WorkContractViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: OrderRepository
    private lateinit var settings: AppSettingsRepository
    private lateinit var snackbar: SnackbarController

    private val acceptanceId = "acceptance-1"

    @Before
    fun setUp() {
        repository = mockk()
        settings = mockk()
        snackbar = mockk(relaxed = true)
        coEvery { settings.emailLanguageTag() } returns "cs"
    }

    private fun viewModel(id: String? = acceptanceId) = WorkContractViewModel(
        orderRepository = repository,
        appSettingsRepository = settings,
        snackbar = snackbar,
        savedStateHandle = SavedStateHandle(mapOf("acceptanceId" to id)),
    )

    private fun contract(language: String = "cs") = WorkContractDto(
        legalDocumentTextId = "text-1",
        version = "2026-09-20",
        language = language,
        title = "Smlouva o dílo",
        contentHtml = "<p>Smlouva vzniká.</p>",
        facts = WorkContractFactsDto(
            orderNumber = "CL-2026-0042",
            cleaningDateTimeUtc = "2026-08-12T09:00:00Z",
            estimatedMinutes = 240,
            totalPrice = 1850.5,
            currencyCode = "CZK",
            locationApproximate = "Praha 4, 140 xx",
            rooms = 3,
            bathrooms = 2,
            services = listOf("Standardní úklid"),
            packages = emptyList(),
            extraSlugs = listOf("inside-oven"),
        ),
        acceptance = WorkContractAcceptanceDetailsDto(
            acceptedOn = "2026-08-10T18:40:00Z",
            documentVersion = "2026-09-20",
            acceptedLanguage = "cs",
        ),
    )

    @Test
    fun `starts loading and lands on the contract read in the UI language`() = runTest {
        coEvery { repository.getWorkContract(acceptanceId, "cs") } returns ApiResult.Success(contract())

        val vm = viewModel()
        assertEquals(WorkContractUiState.Loading, vm.state.value)

        advanceUntilIdle()

        assertEquals(WorkContractUiState.Loaded(contract()), vm.state.value)
        coVerify(exactly = 1) { repository.getWorkContract(acceptanceId, "cs") }
    }

    @Test
    fun `a refused read is the error state with nothing of the order and the refusal on the snackbar`() = runTest {
        val refusal = ApiError.BadRequest("Booking not found.", errorKey = "order.not_found")
        coEvery { repository.getWorkContract(acceptanceId, "cs") } returns ApiResult.Error(refusal)

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(WorkContractUiState.Error, vm.state.value)
        verify(exactly = 1) { snackbar.showError(refusal) }
    }

    @Test
    fun `a network failure is the error state with no snackbar of its own`() = runTest {
        coEvery { repository.getWorkContract(acceptanceId, "cs") } returns
            ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(WorkContractUiState.Error, vm.state.value)
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `retry re-reads the same acceptance in the language current at that moment`() = runTest {
        coEvery { repository.getWorkContract(acceptanceId, "cs") } returns ApiResult.Error(ApiError.Network("offline"))
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(WorkContractUiState.Error, vm.state.value)

        coEvery { settings.emailLanguageTag() } returns "en"
        coEvery { repository.getWorkContract(acceptanceId, "en") } returns ApiResult.Success(contract(language = "en"))
        vm.refresh()
        assertEquals(WorkContractUiState.Loading, vm.state.value)
        advanceUntilIdle()

        assertEquals(WorkContractUiState.Loaded(contract(language = "en")), vm.state.value)
        coVerify(exactly = 1) { repository.getWorkContract(acceptanceId, "en") }
    }

    @Test
    fun `a missing acceptance id is the error state and asks the server nothing`() = runTest {
        val vm = viewModel(id = null)
        advanceUntilIdle()

        assertEquals(WorkContractUiState.Error, vm.state.value)
        coVerify(exactly = 0) { repository.getWorkContract(any(), any()) }
    }
}
