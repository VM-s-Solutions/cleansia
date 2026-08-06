package cz.cleansia.customer.features.recurring

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

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
    )

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        membershipStaleness = Staleness()
        membership = MutableStateFlow(null)

        every { repository.templates } returns MutableStateFlow(listOf(template))
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
    }

    private fun membershipResponse(hasMembership: Boolean) =
        GetMyMembershipResponse(hasMembership = hasMembership)

    private fun newViewModel() = RecurringBookingsViewModel(repository, membershipRepository)

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
}
