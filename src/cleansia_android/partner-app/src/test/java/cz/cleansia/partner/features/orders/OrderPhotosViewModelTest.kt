package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.GetOrderPhotosResponse
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class OrderPhotosViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController

    private val orderId = "order-1"
    private val photo = mockk<GetOrderPhotosOrderPhotoDto> {
        every { photoType } returns PhotoType._1
        every { id } returns "photo-1"
    }

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() =
        OrderPhotosViewModel(SavedStateHandle(mapOf("orderId" to orderId)), ordersRepository, errorTranslator, snackbar)

    private fun responseWith(vararg photos: GetOrderPhotosOrderPhotoDto) =
        GetOrderPhotosResponse(photos = photos.toList())

    @Test
    fun `init loads photos transitioning Loading to Loaded`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))

        val vm = viewModel()
        assertEquals(OrderPhotosUiState.Loading, vm.uiState.value)

        advanceUntilIdle()
        assertEquals(OrderPhotosUiState.Loaded(listOf(photo)), vm.uiState.value)
        assertEquals(0, vm.mutationVersion.value)
    }

    @Test
    fun `init failure transitions to Error and snackbars`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertTrue(vm.uiState.value is OrderPhotosUiState.Error)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `successful upload bumps mutationVersion clears uploading and refreshes`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery {
            ordersRepository.uploadPhoto(any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.upload(PhotoType._1, "p.jpg", "image/jpeg", "data")
        advanceUntilIdle()

        assertEquals(1, vm.mutationVersion.value)
        assertEquals(false, vm.mutation.value.isUploading)
        assertEquals(OrderPhotosUiState.Loaded(listOf(photo)), vm.uiState.value)
    }

    @Test
    fun `upload failure clears uploading without bumping version and snackbars`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery {
            ordersRepository.uploadPhoto(any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.upload(PhotoType._1, "p.jpg", "image/jpeg", "data")
        advanceUntilIdle()

        assertEquals(0, vm.mutationVersion.value)
        assertEquals(false, vm.mutation.value.isUploading)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `successful delete bumps mutationVersion clears deletingId and refreshes`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery { ordersRepository.deletePhoto("photo-1") } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.delete("photo-1")
        advanceUntilIdle()

        assertEquals(1, vm.mutationVersion.value)
        assertNull(vm.mutation.value.deletingId)
    }

    @Test
    fun `delete failure clears deletingId without bumping version and snackbars`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery { ordersRepository.deletePhoto("photo-1") } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.delete("photo-1")
        advanceUntilIdle()

        assertEquals(0, vm.mutationVersion.value)
        assertNull(vm.mutation.value.deletingId)
        verify { snackbar.showError("translated error") }
    }
}
