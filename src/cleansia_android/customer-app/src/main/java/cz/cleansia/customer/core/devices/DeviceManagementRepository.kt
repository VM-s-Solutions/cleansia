package cz.cleansia.customer.core.devices

import android.content.Context
import cz.cleansia.core.auth.DeviceIdProvider
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.auth.ApiErrorParser
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Device self-service over GET /api/Device/Mine + DELETE /api/Device/{rowId}.
 * Sends THIS install's stable device id as `currentDeviceId` so the backend
 * can flag the row the user is holding (the screen hides revoke for it).
 * Stateless — nothing cached, so no [cz.cleansia.core.auth.SessionScopedCache].
 *
 * Customer-app repo contract: `null`/false = failure, already snackbarred here.
 */
@Singleton
class DeviceManagementRepository @Inject constructor(
    private val api: DeviceManagementApi,
    private val deviceIdProvider: DeviceIdProvider,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) {

    suspend fun getMyDevices(): List<UserDeviceDto>? {
        val resp = networkCall(TAG) { api.mine(deviceIdProvider.deviceId) } ?: return null
        if (!resp.isSuccessful) {
            snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code()))
            return null
        }
        return resp.body().orEmpty()
    }

    /** Revoke by the server-side row id — kills push + that device's session. */
    suspend fun revoke(deviceRowId: String): Boolean {
        val resp = networkCall(TAG) { api.revoke(deviceRowId) } ?: return false
        if (!resp.isSuccessful) {
            snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code()))
            return false
        }
        return resp.body()?.success ?: true
    }

    private companion object {
        const val TAG = "DeviceManagementRepository"
    }
}
