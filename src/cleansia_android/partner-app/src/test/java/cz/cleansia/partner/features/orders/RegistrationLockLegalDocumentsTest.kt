package cz.cleansia.partner.features.orders

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.LegalDocumentType
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.CleanerLegalDocument
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.navigation.NavRoute
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Rule
import org.junit.Test

/**
 * An administrator cannot approve a cleaner who has not accepted the contract documents in force, so
 * the lock shows them as a step of their own — and only while one is in force, since until then
 * approval waits on nothing.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class RegistrationLockLegalDocumentsTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val readyForReview = RegistrationCompletionStatus(
        areDocumentsUploaded = true,
        hasCompletedProfile = true,
        missingFields = emptyList(),
        contractStatus = ContractStatus._1,
    )

    private fun document(accepted: Boolean) = CleanerLegalDocument(
        type = LegalDocumentType._3,
        legalDocumentTextId = "text-1",
        version = "2026-12-01",
        title = "Rámcová smlouva",
        contentHtml = "<p>text</p>",
        isAccepted = accepted,
        acceptedVersion = null,
        acceptedAt = null,
    )

    private fun List<StepRow>.row(category: StepCategory) = firstOrNull { it.category == category }

    @Test
    fun `with nothing in force there is no documents step and approval waits on nothing`() {
        listOf(null, emptyList<CleanerLegalDocument>()).forEach { documents ->
            val steps = RegistrationLockViewModel.buildSteps(readyForReview, documents)

            assertNull(steps.row(StepCategory.LegalDocuments))
            assertEquals(StepStatus.Pending, steps.row(StepCategory.Approval)?.status)
        }
    }

    @Test
    fun `an unaccepted document is a missing step that leads to the documents and holds approval back`() {
        val steps = RegistrationLockViewModel.buildSteps(readyForReview, listOf(document(accepted = true), document(accepted = false)))

        val row = steps.row(StepCategory.LegalDocuments)
        assertEquals(StepStatus.Missing, row?.status)
        assertEquals(NavRoute.LegalDocuments, row?.fixDestination)
        assertEquals(StepStatus.Missing, steps.row(StepCategory.Approval)?.status)
        assertEquals(
            listOf(StepCategory.Profile, StepCategory.Documents, StepCategory.LegalDocuments, StepCategory.Approval),
            steps.map { it.category },
        )
    }

    @Test
    fun `every document accepted is a done step and the application waits for review`() {
        val steps = RegistrationLockViewModel.buildSteps(readyForReview, listOf(document(accepted = true)))

        assertEquals(StepStatus.Done, steps.row(StepCategory.LegalDocuments)?.status)
        assertNull(steps.row(StepCategory.LegalDocuments)?.fixDestination)
        assertEquals(StepStatus.Pending, steps.row(StepCategory.Approval)?.status)
    }

    @Test
    fun `the lock reads the documents with the status and a failed read shows the banner without losing the status`() = runTest {
        val profileRepository: ProfileRepository = mockk()
        val appSettings: AppSettingsRepository = mockk(relaxed = true)
        val errors: ApiErrorTranslator = mockk()
        coEvery { appSettings.emailLanguageTag() } returns "cs"
        every { errors.translate(any()) } returns "translated"
        every { profileRepository.getRegistrationStatusStaleness() } returns Staleness()
        coEvery { profileRepository.getRegistrationStatus() } returns ApiResult.Success(readyForReview)
        coEvery { profileRepository.getLegalDocuments("cs") } returnsMany listOf(
            ApiResult.Success(listOf(document(accepted = false))),
            ApiResult.Error(ApiError.Network("down")),
        )
        val vm = RegistrationLockViewModel(
            profileRepository = profileRepository,
            authRepository = mockk<AuthRepository>(relaxed = true),
            errorTranslator = errors,
            appSettingsRepository = appSettings,
            languageSync = mockk(relaxed = true),
        )
        advanceUntilIdle()

        assertEquals(listOf(document(accepted = false)), vm.uiState.value.legalDocuments)
        assertNull(vm.uiState.value.errorMessage)

        vm.userRefresh()
        advanceUntilIdle()

        assertEquals(listOf(document(accepted = false)), vm.uiState.value.legalDocuments)
        assertEquals(readyForReview, vm.uiState.value.status)
        assertEquals("translated", vm.uiState.value.errorMessage)
    }
}
