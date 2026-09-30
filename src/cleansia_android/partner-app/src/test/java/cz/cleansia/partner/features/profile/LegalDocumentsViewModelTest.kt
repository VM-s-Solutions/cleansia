package cz.cleansia.partner.features.profile

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.LegalDocumentType
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.profile.CleanerLegalDocument
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LegalDocumentsViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var appSettings: AppSettingsRepository
    private lateinit var errors: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController

    @Before
    fun setUp() {
        repository = mockk()
        appSettings = mockk()
        errors = mockk()
        snackbar = mockk(relaxed = true)
        coEvery { appSettings.emailLanguageTag() } returns "cs"
        every { errors.translate(any()) } returns "translated"
    }

    private fun viewModel() = LegalDocumentsViewModel(repository, appSettings, errors, snackbar)

    private fun document(textId: String, accepted: Boolean = false) = CleanerLegalDocument(
        type = LegalDocumentType._3,
        legalDocumentTextId = textId,
        version = "2026-12-01",
        title = "Rámcová smlouva",
        contentHtml = "<p>text</p>",
        isAccepted = accepted,
        acceptedVersion = if (accepted) "2026-12-01" else null,
        acceptedAt = if (accepted) "2026-12-02T09:00:00+00:00" else null,
    )

    private fun badRequest(key: String) = ApiError.BadRequest(
        message = "A validation problem occurred.",
        validationErrors = mapOf("AcceptedTextId" to listOf(key)),
        errorKey = key,
    )

    @Test
    fun `opening reads the documents in the app's language`() = runTest {
        val documents = listOf(document("text-1"))
        coEvery { repository.getLegalDocuments("cs") } returns ApiResult.Success(documents)

        val vm = viewModel()
        assertEquals(LegalDocumentsUiState.Loading, vm.uiState.value)
        advanceUntilIdle()

        assertEquals(LegalDocumentsUiState.Loaded(documents), vm.uiState.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
    }

    @Test
    fun `nothing in force is an empty list, not an error`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returns ApiResult.Success(emptyList())

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(LegalDocumentsUiState.Loaded(emptyList()), vm.uiState.value)
    }

    @Test
    fun `a failed first read is the error state and retry reads again`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returnsMany listOf(
            ApiResult.Error(ApiError.Network("down")),
            ApiResult.Success(listOf(document("text-1"))),
        )
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(LegalDocumentsUiState.Error, vm.uiState.value)

        vm.retry()
        advanceUntilIdle()

        assertEquals(LegalDocumentsUiState.Loaded(listOf(document("text-1"))), vm.uiState.value)
    }

    @Test
    fun `accepting echoes the text read, confirms, closes the document and re-reads the list`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returnsMany listOf(
            ApiResult.Success(listOf(document("text-1"))),
            ApiResult.Success(listOf(document("text-1", accepted = true))),
        )
        coEvery { repository.acceptLegalDocument("text-1") } returns ApiResult.Success(Unit)
        val vm = viewModel()
        val closed = mutableListOf<LegalDocumentType>()
        val job = launch { vm.accepted.collect { closed += it } }
        advanceUntilIdle()

        vm.accept(document("text-1"))
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.acceptLegalDocument("text-1") }
        verify(exactly = 1) { snackbar.showSuccessKey(R.string.legal_documents_accepted_toast) }
        assertEquals(listOf(LegalDocumentType._3), closed)
        assertEquals(LegalDocumentsUiState.Loaded(listOf(document("text-1", accepted = true))), vm.uiState.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
        job.cancel()
    }

    @Test
    fun `a second accept while one is in flight is refused`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returns ApiResult.Success(listOf(document("text-1")))
        coEvery { repository.acceptLegalDocument("text-1") } coAnswers {
            delay(1_000)
            ApiResult.Success(Unit)
        }
        val vm = viewModel()
        advanceUntilIdle()

        vm.accept(document("text-1"))
        vm.accept(document("text-1"))
        advanceTimeBy(500)

        assertEquals(ActionState.Submitting, vm.actionState.value)
        advanceUntilIdle()
        coVerify(exactly = 1) { repository.acceptLegalDocument("text-1") }
    }

    /**
     * A newer version came into force while the old text was on screen. The list is re-read so the open
     * document shows the new text, and the cleaner is told to read it before accepting — never the old
     * text accepted, never the document closed as if it were done.
     */
    @Test
    fun `a text no longer in force re-reads the list, shows the notice and keeps the document open`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returnsMany listOf(
            ApiResult.Success(listOf(document("text-1"))),
            ApiResult.Success(listOf(document("text-2"))),
        )
        coEvery { repository.acceptLegalDocument("text-1") } returns
            ApiResult.Error(badRequest("legal.document_not_in_force"))
        val vm = viewModel()
        val closed = mutableListOf<LegalDocumentType>()
        val job = launch { vm.accepted.collect { closed += it } }
        advanceUntilIdle()

        vm.accept(document("text-1"))
        advanceUntilIdle()

        coVerify(exactly = 2) { repository.getLegalDocuments("cs") }
        assertEquals(LegalDocumentsUiState.Loaded(listOf(document("text-2"))), vm.uiState.value)
        assertEquals(LegalDocumentNotice.TextUpdated, vm.notice.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
        assertTrue("the document must stay open on a newer version", closed.isEmpty())
        job.cancel()
    }

    @Test
    fun `any other refusal is shown on the document and nothing is re-read`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returns ApiResult.Success(listOf(document("text-1")))
        coEvery { repository.acceptLegalDocument("text-1") } returns ApiResult.Error(ApiError.Network("down"))
        val vm = viewModel()
        advanceUntilIdle()

        vm.accept(document("text-1"))
        advanceUntilIdle()

        assertEquals(ActionState.Error("translated"), vm.actionState.value)
        assertNull(vm.notice.value)
        coVerify(exactly = 1) { repository.getLegalDocuments("cs") }
    }

    @Test
    fun `closing the document clears what it left behind`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returnsMany listOf(
            ApiResult.Success(listOf(document("text-1"))),
            ApiResult.Success(listOf(document("text-2"))),
        )
        coEvery { repository.acceptLegalDocument("text-1") } returns
            ApiResult.Error(badRequest("legal.document_not_in_force"))
        val vm = viewModel()
        advanceUntilIdle()
        vm.accept(document("text-1"))
        advanceUntilIdle()

        vm.onDocumentClosed()

        assertNull(vm.notice.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
    }

    @Test
    fun `a failed re-read keeps the list on screen`() = runTest {
        coEvery { repository.getLegalDocuments("cs") } returnsMany listOf(
            ApiResult.Success(listOf(document("text-1"))),
            ApiResult.Error(ApiError.Network("down")),
        )
        coEvery { repository.acceptLegalDocument("text-1") } returns ApiResult.Success(Unit)
        val vm = viewModel()
        advanceUntilIdle()

        vm.accept(document("text-1"))
        advanceUntilIdle()

        assertEquals(LegalDocumentsUiState.Loaded(listOf(document("text-1"))), vm.uiState.value)
        verify(exactly = 1) { snackbar.showError("translated") }
    }
}
