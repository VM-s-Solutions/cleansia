package cz.cleansia.partner.features.orders

import android.content.ContentResolver
import android.content.Context
import android.net.Uri
import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.media.Base64Image
import cz.cleansia.core.media.ImageCompressor
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.GetOrderPhotosResponse
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.mockkObject
import io.mockk.unmockkObject
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.After
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
    private lateinit var appContext: Context

    private val orderId = "order-1"
    private val pickedUri = mockk<Uri>()
    private val photo = mockk<GetOrderPhotosOrderPhotoDto> {
        every { photoType } returns PhotoType._1
        every { id } returns "photo-1"
    }

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.contentResolver } returns mockk<ContentResolver>(relaxed = true)
        every { appContext.getString(R.string.photo_encode_failed) } returns "encode failed"

        // The bitmap pipeline itself is not exercisable on a plain JVM (no
        // Robolectric, and partner-app does not stub android.* returns), so the
        // shared compressor is mocked and this test covers what the VM does
        // with its two outcomes.
        mockkObject(ImageCompressor)
        coEvery { ImageCompressor.compressToBase64(any(), any(), any(), any()) } returns
            Base64Image(base64 = "data", contentType = "image/jpeg", fileName = "photo.jpg")
    }

    @After
    fun tearDown() {
        unmockkObject(ImageCompressor)
    }

    private fun viewModel() = OrderPhotosViewModel(
        SavedStateHandle(mapOf("orderId" to orderId)),
        ordersRepository,
        errorTranslator,
        snackbar,
        appContext,
    )

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

        vm.compressAndUpload(PhotoType._1, pickedUri)
        advanceUntilIdle()

        assertEquals(1, vm.mutationVersion.value)
        assertEquals(false, vm.mutation.value.isUploading)
        assertEquals(OrderPhotosUiState.Loaded(listOf(photo)), vm.uiState.value)
    }

    /**
     * The compressor's output, not the picker's, is what reaches the wire. This
     * is the metadata strip: anything else means the raw EXIF-bearing bytes
     * still ship.
     */
    @Test
    fun `upload sends the compressor's jpeg rather than the picked file`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery {
            ordersRepository.uploadPhoto(any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.compressAndUpload(PhotoType._1, pickedUri)
        advanceUntilIdle()

        coVerify(exactly = 1) {
            ordersRepository.uploadPhoto(orderId, PhotoType._1, "photo.jpg", "image/jpeg", "data")
        }
    }

    /**
     * An undecodable pick (corrupt file, HEIC below API 28) has to reset the
     * add-tile spinner AND say something — it has been spinning since the tap,
     * so a silent reset reads as the app ignoring the user.
     */
    @Test
    fun `compression failure snackbars clears uploading and never hits the network`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery { ImageCompressor.compressToBase64(any(), any(), any(), any()) } returns null

        val vm = viewModel()
        advanceUntilIdle()

        vm.compressAndUpload(PhotoType._1, pickedUri)
        advanceUntilIdle()

        verify { snackbar.showError("encode failed") }
        assertEquals(false, vm.mutation.value.isUploading)
        assertEquals(0, vm.mutationVersion.value)
        coVerify(exactly = 0) { ordersRepository.uploadPhoto(any(), any(), any(), any(), any()) }
    }

    /**
     * The spinner replaces the add tile, but a fast double-tap can still land
     * two picks. Matches the iOS guard in `OrderPhotosViewModel.swift`.
     */
    @Test
    fun `a second upload while one is in flight is dropped`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery {
            ordersRepository.uploadPhoto(any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.compressAndUpload(PhotoType._1, pickedUri)
        vm.compressAndUpload(PhotoType._2, pickedUri)
        advanceUntilIdle()

        assertEquals(1, vm.mutationVersion.value)
        coVerify(exactly = 1) { ordersRepository.uploadPhoto(any(), any(), any(), any(), any()) }
    }

    @Test
    fun `upload failure clears uploading without bumping version and snackbars`() = runTest {
        coEvery { ordersRepository.getPhotos(orderId) } returns ApiResult.Success(responseWith(photo))
        coEvery {
            ordersRepository.uploadPhoto(any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.compressAndUpload(PhotoType._1, pickedUri)
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
