package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.navigation.NavRoute
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Until approval the lock replaces the whole app, so a row that went Done must stay a way back in:
 * otherwise a cleaner who finished their profile could never correct it, and one who uploaded a single
 * document could never add the second their country requires. -> /partner-app/onboarding
 */
class RegistrationLockStepsTest {

    private fun status(contract: ContractStatus) = RegistrationCompletionStatus(
        areDocumentsUploaded = true,
        hasCompletedProfile = true,
        missingFields = emptyList(),
        contractStatus = contract,
    )

    private fun List<StepRow>.row(category: StepCategory) = first { it.category == category }

    @Test
    fun `a done profile reopens the chain on Personal`() {
        val row = RegistrationLockViewModel.buildSteps(status(ContractStatus._1)).row(StepCategory.Profile)

        assertEquals(StepStatus.Done, row.status)
        assertEquals(NavRoute.ProfilePersonal(onboarding = true), row.fixDestination)
    }

    @Test
    fun `done documents reopen the documents screen`() {
        val row = RegistrationLockViewModel.buildSteps(status(ContractStatus._1)).row(StepCategory.Documents)

        assertEquals(StepStatus.Done, row.status)
        assertEquals(NavRoute.ProfileDocuments, row.fixDestination)
    }

    @Test
    fun `a rejected cleaner can still reopen every section`() {
        val steps = RegistrationLockViewModel.buildSteps(status(ContractStatus._5))

        assertEquals(NavRoute.ProfilePersonal(onboarding = true), steps.row(StepCategory.Profile).fixDestination)
        assertEquals(NavRoute.ProfileDocuments, steps.row(StepCategory.Documents).fixDestination)
    }

    @Test
    fun `an incomplete profile still opens on the first missing section`() {
        val row = RegistrationLockViewModel.buildSteps(
            RegistrationCompletionStatus(
                hasCompletedProfile = false,
                missingFields = listOf("profile.fields.iban"),
                contractStatus = ContractStatus._1,
            ),
        ).row(StepCategory.Profile)

        assertEquals(StepStatus.Missing, row.status)
        assertEquals(NavRoute.ProfileBank(onboarding = true), row.fixDestination)
    }

    @Test
    fun `a rejected application carries the administrator's reason, trimmed`() {
        val row = RegistrationLockViewModel.buildSteps(
            status(ContractStatus._5).copy(rejectionReason = "  The ID photo is unreadable.\n"),
        ).row(StepCategory.Approval)

        assertEquals(StepStatus.Missing, row.status)
        assertEquals(listOf("registration_lock.approval_rejected"), row.detailKeys)
        assertEquals("The ID photo is unreadable.", row.note)
    }

    @Test
    fun `no reason, a blank one, or a decision other than rejection carries no note`() {
        listOf(null, "", "   ").forEach { reason ->
            val row = RegistrationLockViewModel.buildSteps(status(ContractStatus._5).copy(rejectionReason = reason))
                .row(StepCategory.Approval)
            assertNull(row.note)
        }
        // A stale reason left on a cleaner who has since been approved, or is pending again, is not shown.
        listOf(ContractStatus._1, ContractStatus._4).forEach { contract ->
            val steps = RegistrationLockViewModel.buildSteps(status(contract).copy(rejectionReason = "old"))
            steps.forEach { assertNull(it.note) }
        }
    }

    @Test
    fun `the approval row never routes anywhere`() {
        listOf(ContractStatus._1, ContractStatus._5).forEach { contract ->
            assertNull(RegistrationLockViewModel.buildSteps(status(contract)).row(StepCategory.Approval).fixDestination)
        }
    }
}
