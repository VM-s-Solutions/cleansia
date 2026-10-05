package cz.cleansia.customer.features.recurring

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.catalog.CatalogApi
import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.data.UserAddress
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.memberships.MembershipStatus
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

/**
 * A customer whose Plus membership lapsed is still being charged for every
 * occurrence this screen's schedules generate, so pause and delete must reach
 * the repository whatever the membership answer is. Authoring is the half the
 * server refuses, and the client mirrors that refusal rather than hiding the
 * stop button behind it.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class RecurringBookingsViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: RecurringBookingRepository
    private lateinit var membershipRepository: MembershipRepository
    private lateinit var membershipStaleness: Staleness
    private lateinit var membership: MutableStateFlow<GetMyMembershipResponse?>
    private lateinit var addressRepository: AddressRepository
    private lateinit var catalogApi: CatalogApi
    private lateinit var templates: MutableStateFlow<List<RecurringBookingTemplateDto>>
    private lateinit var addresses: MutableStateFlow<List<UserAddress>>

    private val template = RecurringBookingTemplateDto(
        id = "tpl-1",
        frequency = 1,
        dayOfWeek = 4,
        timeOfDay = "10:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId = "addr-1",
        addressLine = "Zenklova 6, Praha",
        selectedServiceIds = listOf("svc-1"),
        selectedPackageIds = emptyList(),
        paymentType = 1,
        startsOn = "2026-08-01T10:00:00Z",
        endsOn = null,
        isActive = true,
        requiresPaymentMethodChange = false,
    )

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        membershipStaleness = Staleness()
        membership = MutableStateFlow(null)

        templates = MutableStateFlow(listOf(template))
        every { repository.templates } returns templates
        every { repository.loading } returns MutableStateFlow(false)
        every { repository.loaded } returns MutableStateFlow(true)
        coEvery { repository.setActive(any(), any()) } returns ApiResult.Success(Unit)
        coEvery { repository.delete(any()) } returns ApiResult.Success(Unit)
        every { membershipRepository.current } returns membership
        every { membershipRepository.staleness } returns membershipStaleness
        coEvery { membershipRepository.refresh() } coAnswers {
            membershipStaleness.markFresh()
            ApiResult.Success(membershipResponse(hasMembership = false))
        }
        addressRepository = mockk()
        addresses = MutableStateFlow(emptyList())
        coEvery { addressRepository.refreshFromServer() } returns ApiResult.Success(Unit)
        every { addressRepository.addresses } returns addresses
        catalogApi = mockk()
    }

    private fun membershipResponse(hasMembership: Boolean) =
        GetMyMembershipResponse(hasMembership = hasMembership)

    private fun newViewModel() = RecurringBookingsViewModel(repository, membershipRepository, addressRepository, catalogApi)

    @Test
    fun `a lapsed member can still pause a schedule that is charging them`() = runTest {
        membership.value = membershipResponse(hasMembership = false)
        val viewModel = newViewModel()
        advanceUntilIdle()

        viewModel.toggleActive("tpl-1", currentlyActive = true)
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.setActive("tpl-1", false) }
    }

    @Test
    fun `a lapsed member can still resume a schedule they paused`() = runTest {
        membership.value = membershipResponse(hasMembership = false)
        val viewModel = newViewModel()
        advanceUntilIdle()

        viewModel.toggleActive("tpl-1", currentlyActive = false)
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.setActive("tpl-1", true) }
    }

    @Test
    fun `a lapsed member can still delete a schedule that is charging them`() = runTest {
        membership.value = membershipResponse(hasMembership = false)
        val viewModel = newViewModel()
        advanceUntilIdle()

        viewModel.delete("tpl-1")
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.delete("tpl-1") }
    }

    @Test
    fun `a lapsed member still sees their schedules`() = runTest {
        membership.value = membershipResponse(hasMembership = false)
        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(listOf(template), viewModel.templates.value)
        assertEquals(RecurringAuthoringGate.Upsell, viewModel.authoring.value)
    }

    @Test
    fun `the screen fetches membership itself rather than trusting another screen's cache`() = runTest {
        newViewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { membershipRepository.refresh() }
    }

    /**
     * The aggravating half of the defect: nothing on this screen fetched membership,
     * so a paid-up member met the wall whenever the value had not landed yet.
     */
    @Test
    fun `an unresolved membership does not withhold authoring`() = runTest {
        coEvery { membershipRepository.refresh() } coAnswers {
            ApiResult.Error(cz.cleansia.core.network.ApiError.Network("offline"))
        }

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Allowed, viewModel.authoring.value)
    }

    @Test
    fun `a member keeps authoring`() = runTest {
        membership.value = membershipResponse(hasMembership = true)
        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(RecurringAuthoringGate.Allowed, viewModel.authoring.value)
    }

    /** The server refuses a past-due member's create and edit and books none of their schedules. */
    @Test
    fun `a past-due member keeps their schedules but may not author them`() = runTest {
        membership.value = GetMyMembershipResponse(hasMembership = true, status = MembershipStatus.PastDue.code)
        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(listOf(template), viewModel.templates.value)
        assertEquals(RecurringAuthoringGate.Paused, viewModel.authoring.value)
    }

    // W1: a schedule holding an entry its market's catalogue no longer lists says so on its card. Only a
    // catalogue read for the schedule's own market judges it; without one the card says nothing.

    private fun address(serverId: String, countryId: String?) = UserAddress(
        id = serverId, serverId = serverId, label = serverId, street = "Hlavná 1", city = "Bratislava",
        zipCode = "81101", countryId = countryId,
    )

    private fun service(id: String) = ServiceListItem(
        id = id, name = "Service $id", basePrice = 10.0, perRoomPrice = 1.0,
        category = CategoryDto(id = "c-1", slug = "general", name = "General"),
    )

    private fun pkg(id: String) = PackageListItem(id = id, name = "Package $id", price = 20.0)

    private fun catalogue(countryId: String?, services: List<String>, packages: List<String>) {
        coEvery { catalogApi.getServices(countryId) } returns Response.success(services.map(::service))
        coEvery { catalogApi.getPackages(countryId) } returns Response.success(packages.map(::pkg))
    }

    @Test
    fun `a schedule holding a service its market no longer lists says so`() = runTest {
        addresses.value = listOf(address("addr-1", "svk-id"))
        catalogue("svk-id", services = listOf("svc-2"), packages = emptyList())

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(setOf("tpl-1"), viewModel.noLongerOffered.value)
    }

    @Test
    fun `a schedule holding a package its market no longer lists says so`() = runTest {
        templates.value = listOf(template.copy(selectedServiceIds = listOf("svc-1"), selectedPackageIds = listOf("pkg-retired")))
        addresses.value = listOf(address("addr-1", "svk-id"))
        catalogue("svk-id", services = listOf("svc-1"), packages = listOf("pkg-1"))

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(setOf("tpl-1"), viewModel.noLongerOffered.value)
    }

    @Test
    fun `a schedule whose every entry is listed says nothing`() = runTest {
        templates.value = listOf(template.copy(selectedPackageIds = listOf("pkg-1")))
        addresses.value = listOf(address("addr-1", "svk-id"))
        catalogue("svk-id", services = listOf("svc-1", "svc-2"), packages = listOf("pkg-1"))

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(emptySet<String>(), viewModel.noLongerOffered.value)
    }

    @Test
    fun `a schedule is judged by its own market's catalogue, the platform default for an address with no country`() = runTest {
        templates.value = listOf(template, template.copy(id = "tpl-2", savedAddressId = "addr-2"))
        addresses.value = listOf(address("addr-1", "svk-id"), address("addr-2", null))
        catalogue("svk-id", services = listOf("svc-1"), packages = emptyList())
        catalogue(null, services = listOf("svc-9"), packages = emptyList())

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertEquals(setOf("tpl-2"), viewModel.noLongerOffered.value)
    }

    @Test
    fun `a schedule whose market's catalogue could not be read is not judged`() = runTest {
        addresses.value = listOf(address("addr-1", "svk-id"))
        coEvery { catalogApi.getServices("svk-id") } returns
            Response.error(500, "{}".toResponseBody("application/problem+json".toMediaType()))
        coEvery { catalogApi.getPackages("svk-id") } returns Response.success(emptyList())

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertTrue(viewModel.noLongerOffered.value.isEmpty())
    }

    @Test
    fun `a schedule whose address could not be read is not judged`() = runTest {
        addresses.value = listOf(address("addr-1", "svk-id"))
        catalogue("svk-id", services = listOf("svc-2"), packages = emptyList())
        coEvery { addressRepository.refreshFromServer() } returns
            ApiResult.Error(cz.cleansia.core.network.ApiError.Network("offline"))

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertTrue(viewModel.noLongerOffered.value.isEmpty())
        coVerify(exactly = 0) { catalogApi.getServices(any()) }
    }

    @Test
    fun `a schedule whose saved address is gone is not judged`() = runTest {
        addresses.value = listOf(address("addr-other", "svk-id"))
        catalogue("svk-id", services = listOf("svc-2"), packages = emptyList())

        val viewModel = newViewModel()
        advanceUntilIdle()

        assertTrue(viewModel.noLongerOffered.value.isEmpty())
    }

    @Test
    fun `no schedules read no catalogue`() = runTest {
        templates.value = emptyList()

        newViewModel()
        advanceUntilIdle()

        coVerify(exactly = 0) { addressRepository.refreshFromServer() }
        coVerify(exactly = 0) { catalogApi.getServices(any()) }
    }

    /** An edit trims the retired entry; the list the customer comes back to drops the line. */
    @Test
    fun `the line goes once the schedule no longer holds the retired entry`() = runTest {
        addresses.value = listOf(address("addr-1", "svk-id"))
        catalogue("svk-id", services = listOf("svc-2"), packages = emptyList())
        val viewModel = newViewModel()
        advanceUntilIdle()
        assertEquals(setOf("tpl-1"), viewModel.noLongerOffered.value)

        templates.value = listOf(template.copy(selectedServiceIds = listOf("svc-2")))
        advanceUntilIdle()

        assertTrue(viewModel.noLongerOffered.value.isEmpty())
    }
}
