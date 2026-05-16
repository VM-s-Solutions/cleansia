package cz.cleansia.customer.core.orders

import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.AssignedEmployeeDto as GenAssignedEmployeeDto
import cz.cleansia.customer.api.model.CancelOrderCommand as GenCancelOrderCommand
import cz.cleansia.customer.api.model.CancelOrderResponse as GenCancelOrderResponse
import cz.cleansia.customer.api.model.Code as GenCode
import cz.cleansia.customer.api.model.ConfirmRecurringOrderCommand as GenConfirmRecurringOrderCommand
import cz.cleansia.customer.api.model.ConfirmRecurringOrderResponse as GenConfirmRecurringOrderResponse
import cz.cleansia.customer.api.model.CurrencyDetailDto as GenCurrencyDetailDto
import cz.cleansia.customer.api.model.CurrencyListItem as GenCurrencyListItem
import cz.cleansia.customer.api.model.GetMyServingCleanersResponse as GenGetMyServingCleanersResponse
import cz.cleansia.customer.api.model.GetOrderPhotosOrderPhotoDto as GenOrderPhotoDto
import cz.cleansia.customer.api.model.GetOrderPhotosResponse as GenGetOrderPhotosResponse
import cz.cleansia.customer.api.model.OrderAddress as GenOrderAddress
import cz.cleansia.customer.api.model.OrderIssueDto as GenOrderIssueDto
import cz.cleansia.customer.api.model.OrderItem as GenOrderItem
import cz.cleansia.customer.api.model.OrderListItem as GenOrderListItem
import cz.cleansia.customer.api.model.OrderNoteDto as GenOrderNoteDto
import cz.cleansia.customer.api.model.OrderReviewDto as GenOrderReviewDto
import cz.cleansia.customer.api.model.OrderStatusTrackDto as GenOrderStatusTrackDto
import cz.cleansia.customer.api.model.PackageDetails as GenPackageDetails
import cz.cleansia.customer.api.model.PackageListItem as GenPackageListItem
import cz.cleansia.customer.api.model.PagedDataOfOrderListItem as GenPagedDataOfOrderListItem
import cz.cleansia.customer.api.model.ServiceDetails as GenServiceDetails
import cz.cleansia.customer.api.model.ServiceListItem as GenServiceListItem
import cz.cleansia.customer.api.model.SubmitOrderReviewCommand as GenSubmitOrderReviewCommand
import cz.cleansia.customer.core.user.CodeDto
import okhttp3.ResponseBody
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenOrderApi] for the customer order
 * screens. The hand-written [OrderListItemDto] / [OrderDetailDto] DTOs carry
 * stable shapes (defaulted primitives, etc.) so screens + view-models don't
 * need to deal with the generated all-nullable wire types.
 *
 * Routes mirror [Cleansia.Web.Customer.Controllers.OrderController]:
 *  - `GET  /api/Order/GetMyOrders`     → paged wrapper `PagedData<OrderListItem>`
 *  - `GET  /api/Order/GetById?OrderId=…` → `OrderItem`
 *  - `POST /api/Order/Cancel`          → `CancelOrder.Response`
 *  - `POST /api/Order/ConfirmRecurring` → `ConfirmRecurringOrder.Response`
 *  - `POST /api/Order/SubmitReview`    → `OrderReviewDto`
 *  - `GET  /api/Order/DownloadReceipt?OrderId=…` → raw PDF bytes (streamed)
 *  - `GET  /api/Order/GetPhotos?OrderId=…` → `GetOrderPhotos.Response`
 *  - `GET  /api/Order/MyServingCleaners` → favorite-cleaner picker source
 */
class OrderApi(
    private val orderApi: GenOrderApi,
) {
    suspend fun getMyOrders(offset: Int = 0, limit: Int = 20): Response<OrderListResponseDto> {
        val raw = orderApi.orderGetMyOrders(offset = offset, limit = limit)
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun getById(id: String): Response<OrderDetailDto> {
        val raw = orderApi.orderGetById(orderId = id)
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun cancel(body: CancelOrderRequest): Response<CancelOrderResponse> {
        val raw = orderApi.orderCancelOrder(
            cancelOrderCommand = GenCancelOrderCommand(orderId = body.orderId, reason = body.reason),
        )
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun confirmRecurring(body: ConfirmRecurringOrderRequest): Response<ConfirmRecurringOrderResponse> {
        val raw = orderApi.orderConfirmRecurring(
            confirmRecurringOrderCommand = GenConfirmRecurringOrderCommand(orderId = body.orderId),
        )
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun submitReview(body: SubmitReviewRequest): Response<OrderReviewDto> {
        val raw = orderApi.orderSubmitReview(
            submitOrderReviewCommand = GenSubmitOrderReviewCommand(
                orderId = body.orderId,
                rating = body.rating,
                comment = body.comment,
            ),
        )
        return raw.mapBody { it?.toAppDto() }
    }

    /**
     * Receipt PDF is returned as raw bytes (`application/pdf`). The generated
     * client already returns `Response<ResponseBody>`; we re-expose without
     * mapping so the repository can keep its streaming-copy logic intact.
     */
    suspend fun downloadReceipt(id: String): Response<ResponseBody> =
        orderApi.orderDownloadReceipt(orderId = id)

    suspend fun getPhotos(id: String): Response<OrderPhotosResponse> {
        val raw = orderApi.orderGetPhotos(orderId = id)
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun getMyServingCleaners(): Response<List<ServingCleanerDto>> {
        val raw = orderApi.orderMyServingCleaners()
        return raw.mapBody { list -> list?.mapNotNull { it.toAppDto() }.orEmpty() }
    }
}

/**
 * Re-wrap a [Response] preserving status + headers but mapping the body.
 * Mapping may legally produce `null` (server gave a malformed payload).
 */
private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → app DTO mappers ───

private fun GenPagedDataOfOrderListItem?.toAppDto(): OrderListResponseDto = OrderListResponseDto(
    pageNumber = this?.pageNumber ?: 0,
    pageSize = this?.pageSize ?: 0,
    total = this?.total ?: 0,
    data = this?.`data`?.map { it.toAppDto() }.orEmpty(),
)

private fun GenOrderListItem.toAppDto(): OrderListItemDto = OrderListItemDto(
    id = id,
    customerName = customerName,
    customerEmail = customerEmail,
    customerPhone = customerPhone,
    customerAddress = customerAddress,
    displayOrderNumber = displayOrderNumber,
    rooms = rooms ?: 0,
    bathrooms = bathrooms ?: 0,
    extras = extras,
    cleaningDateTime = cleaningDateTime?.toString(),
    paymentType = paymentType?.toAppDto(),
    paymentStatus = paymentStatus?.toAppDto(),
    totalPrice = totalPrice ?: 0.0,
    originalSubtotal = originalSubtotal ?: 0.0,
    appliedDiscountSource = appliedDiscountSource?.value ?: 0,
    tierDiscountAmount = tierDiscountAmount,
    membershipDiscountAmount = membershipDiscountAmount,
    promoDiscountAmount = promoDiscountAmount,
    estimatedTime = estimatedTime ?: 0,
    orderStatus = orderStatus?.toAppDto(),
    confirmationCode = confirmationCode,
    stripeSessionId = null, // not exposed on generated OrderListItem
    selectedPackages = selectedPackages?.map { it.toListSummary() },
    currencyId = currencyId,
    currency = currency?.toAppDto(),
    assignedEmployees = assignedEmployees,
    selectedServices = selectedServices?.map { it.toListSummary() },
    requiredEmployees = requiredEmployees ?: 0,
    maxEmployees = maxEmployees ?: 0,
    availableSpots = availableSpots ?: 0,
    assignedEmployeesCount = assignedEmployeesCount ?: 0,
    hasAvailableSpots = hasAvailableSpots ?: false,
)

private fun GenOrderItem.toAppDto(): OrderDetailDto = OrderDetailDto(
    id = id,
    displayOrderNumber = displayOrderNumber,
    customerName = customerName,
    customerEmail = customerEmail,
    customerPhone = customerPhone,
    address = address?.toAppDto(),
    rooms = rooms ?: 0,
    bathrooms = bathrooms ?: 0,
    extras = extras,
    cleaningDateTime = cleaningDateTime?.toString(),
    paymentType = paymentType?.toAppDto(),
    paymentStatus = paymentStatus?.toAppDto(),
    totalPrice = totalPrice ?: 0.0,
    originalSubtotal = originalSubtotal ?: 0.0,
    appliedDiscountSource = appliedDiscountSource?.value ?: 0,
    tierDiscountAmount = tierDiscountAmount,
    membershipDiscountAmount = membershipDiscountAmount,
    promoDiscountAmount = promoDiscountAmount,
    estimatedTime = estimatedTime ?: 0,
    actualCompletionTime = actualCompletionTime,
    completionNotes = completionNotes,
    orderStatus = orderStatus?.toAppDto(),
    confirmationCode = confirmationCode,
    stripeSessionId = null, // not exposed on generated OrderItem
    notes = notes,
    specialInstructions = specialInstructions,
    accessInstructions = accessInstructions,
    recurringTemplateId = recurringTemplateId,
    selectedPackages = selectedPackages?.map { it.toAppDto() },
    currency = currency?.toAppDto(),
    selectedServices = selectedServices?.map { it.toAppDto() },
    statusHistory = statusHistory?.map { it.toAppDto() },
    createdOn = createdOn?.toString(),
    updatedOn = updatedOn?.toString(),
    assignedEmployees = assignedEmployees?.map { it.toAppDto() },
    receiptNumber = receiptNumber,
    orderNotes = orderNotes?.map { it.toAppDto() },
    orderIssues = orderIssues?.map { it.toAppDto() },
    review = review?.toAppDto(),
)

private fun GenCode.toAppDto(): CodeDto = CodeDto(
    type = type.orEmpty(),
    name = name.orEmpty(),
    value = `value` ?: 0,
)

private fun GenOrderAddress.toAppDto(): OrderAddressDto = OrderAddressDto(
    street = street,
    city = city,
    zipCode = zipCode,
    country = country,
)

private fun GenOrderStatusTrackDto.toAppDto(): OrderStatusTrackDto = OrderStatusTrackDto(
    status = status?.toAppDto(),
    createdOn = createdOn?.toString(),
)

private fun GenAssignedEmployeeDto.toAppDto(): AssignedEmployeeDto = AssignedEmployeeDto(
    id = id,
    employeeId = employeeId,
    fullName = fullName,
    phoneNumber = phoneNumber,
    email = null, // not on generated DTO
)

private fun GenOrderReviewDto.toAppDto(): OrderReviewDto = OrderReviewDto(
    id = id,
    orderId = orderId,
    userId = null, // not on generated DTO
    rating = rating ?: 0,
    comment = comment,
    createdOn = createdOn?.toString(),
    updatedOn = updatedOn?.toString(),
)

private fun GenOrderNoteDto.toAppDto(): OrderNoteDto = OrderNoteDto(
    id = id,
    employeeId = employeeId,
    content = content,
    createdOn = createdOn?.toString(),
)

private fun GenOrderIssueDto.toAppDto(): OrderIssueDto = OrderIssueDto(
    id = id,
    reportedByEmployeeId = reportedByEmployeeId,
    description = description,
    isResolved = isResolved ?: false,
    resolvedAt = resolvedAt?.toString(),
    createdOn = createdOn?.toString(),
)

private fun GenServiceListItem.toListSummary(): OrderServiceSummaryDto = OrderServiceSummaryDto(
    id = id,
    name = name,
    description = description,
    basePrice = basePrice ?: 0.0,
    perRoomPrice = perRoomPrice ?: 0.0,
)

private fun GenServiceDetails.toAppDto(): OrderServiceDetailsDto = OrderServiceDetailsDto(
    id = id,
    name = name,
    description = description,
    estimatedTime = estimatedTime ?: 0,
    currencyCode = currencyCode,
)

private fun GenPackageListItem.toListSummary(): OrderPackageSummaryDto = OrderPackageSummaryDto(
    id = id,
    name = name,
    description = description,
    price = price ?: 0.0,
)

private fun GenPackageDetails.toAppDto(): OrderPackageDetailsDto = OrderPackageDetailsDto(
    id = id,
    name = name,
    description = description,
    price = price ?: 0.0,
    estimatedTime = estimatedTime ?: 0,
    currencyCode = currencyCode,
    includedServices = includedServices,
)

private fun GenCurrencyListItem.toAppDto(): OrderCurrencyListItemDto = OrderCurrencyListItemDto(
    id = id,
    code = code,
    symbol = symbol,
    name = name,
    exchangeRate = exchangeRate ?: 0.0,
    isDefault = isDefault ?: false,
)

private fun GenCurrencyDetailDto.toAppDto(): OrderCurrencyDetailDto = OrderCurrencyDetailDto(
    id = id,
    code = code,
    name = name,
    symbol = symbol,
    exchangeRate = exchangeRate ?: 0.0,
    isDefault = isDefault ?: false,
)

private fun GenCancelOrderResponse.toAppDto(): CancelOrderResponse = CancelOrderResponse(
    orderId = orderId,
    feeRate = feeRate ?: 0.0,
    refundAmount = refundAmount ?: 0.0,
    totalPrice = totalPrice ?: 0.0,
    refundInitiated = refundInitiated ?: false,
)

private fun GenConfirmRecurringOrderResponse.toAppDto(): ConfirmRecurringOrderResponse = ConfirmRecurringOrderResponse(
    orderId = orderId,
    clientSecret = clientSecret,
    paymentIntentId = paymentIntentId,
    stripeCustomerId = stripeCustomerId,
    ephemeralKey = ephemeralKey,
)

private fun GenGetOrderPhotosResponse?.toAppDto(): OrderPhotosResponse = OrderPhotosResponse(
    photos = this?.photos?.map { it.toAppDto() }.orEmpty(),
    beforePhotoCount = this?.beforePhotoCount ?: 0,
    afterPhotoCount = this?.afterPhotoCount ?: 0,
)

private fun GenOrderPhotoDto.toAppDto(): OrderPhotoDto = OrderPhotoDto(
    id = id,
    photoType = photoType?.value,
    blobUrl = blobUrl,
    fileName = fileName,
    originalFileName = originalFileName,
    fileSizeBytes = fileSizeBytes ?: 0L,
    contentType = contentType,
    capturedAt = capturedAt?.toString(),
    capturedByEmployeeId = capturedByEmployeeId,
    capturedByEmployeeName = capturedByEmployeeName,
    width = width,
    height = height,
    notes = notes,
)

private fun GenGetMyServingCleanersResponse.toAppDto(): ServingCleanerDto? {
    val employeeId = employeeId ?: return null
    val fullName = fullName ?: return null
    val lastServedOn = lastServedOn?.toString() ?: return null
    return ServingCleanerDto(
        employeeId = employeeId,
        fullName = fullName,
        lastServedOn = lastServedOn,
    )
}
