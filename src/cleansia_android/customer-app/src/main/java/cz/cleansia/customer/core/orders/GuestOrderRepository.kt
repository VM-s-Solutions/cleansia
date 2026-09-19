package cz.cleansia.customer.core.orders

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.network.requiredBody
import cz.cleansia.core.network.wireResult
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import retrofit2.Response

class GuestOrderRepository @Inject constructor(
    private val api: GuestOrderApi,
    @ApplicationContext private val context: Context,
) {
    suspend fun lookup(number: String, email: String, code: String): ApiResult<GuestOrderDto> =
        request { api.lookup(number, email, code) }

    suspend fun preview(number: String, email: String, code: String): ApiResult<CancellationFeePreviewDto> =
        request { api.preview(number, email, code) }

    suspend fun cancel(
        number: String,
        email: String,
        code: String,
        reason: String?,
        language: String,
    ): ApiResult<CancelOrderResponse> =
        request { api.cancel(number, email, code, reason, language) }

    private suspend fun <T : Any> request(call: suspend () -> Response<T>): ApiResult<T> = wireResult {
        val response = networkCall { call() }
            ?: return ApiResult.Error(ApiError.Network(context.getString(R.string.error_generic_network)))
        if (response.isSuccessful) return ApiResult.Success(response.requiredBody())
        val message = ApiErrorParser.parseToUserMessage(context, response.errorBody(), response.code())
        ApiResult.Error(
            when (response.code()) {
                400 -> ApiError.BadRequest(message)
                404 -> ApiError.NotFound(message)
                in 500..599 -> ApiError.Server(response.code(), message)
                else -> ApiError.Unknown(message)
            },
        )
    }
}
