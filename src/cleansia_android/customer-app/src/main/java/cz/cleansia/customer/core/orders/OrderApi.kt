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
import cz.cleansia.customer.api.model.GetCancellationFeePreviewResponse as GenGetCancellationFeePreviewResponse
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
 *  - `GET  /api/Order/CancellationPreview?OrderId=…` → `GetCancellationFeePreview.Response`
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

    suspend fun getCancellationPreview(id: String): Response<CancellationFeePreviewDto> {
        val raw = orderApi.orderCancellationPreview(orderId = id)
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

/**
 * `total` drives "load more": a defaulted zero silently ends pagination, so the customer's older
 * orders stop existing rather than fail to load. A null wrapper is a 200 with no page in it and is
 * refused for the same reason.
 */
private fun GenPagedDataOfOrderListItem?.toAppDto(): OrderListResponseDto? {
    if (this == null) return null
    return OrderListResponseDto(
        pageNumber = pageNumber ?: return null,
        pageSize = pageSize ?: return null,
        total = total ?: return null,
        // The two rulings, each where it is decided: an unidentifiable row is dropped, a row whose own
        // money is broken refuses the page.
        data = `data`.orEmpty()
            .filter { it.id != null }
            .map { it.toAppDtoOrRefuse() ?: return null },
    )
}

/**
 * Drops the unidentifiable row rather than refusing the page: no customer surface sums this list —
 * the paged `total` is the server's own count and the tab badges count the rows actually shown — so a
 * lost row cannot falsify a figure, while refusing the page would hide every other order the server
 * answered correctly. An id-less row was already dead, since the card navigates by id. Where a
 * collection *is* the addends of a rendered total the ruling inverts; see
 * [cz.cleansia.customer.core.catalog.toAppDto].
 *
 * That is the identity half only. A surviving row's own money still refuses, and because the row is
 * an element of the page, refusing it refuses the page — an order is either priced as the server
 * priced it or the list says it could not be loaded.
 */
private fun GenOrderListItem.toAppDtoOrRefuse(): OrderListItemDto? {
    return OrderListItemDto(
        id = id ?: return null,
        customerName = customerName,
        customerEmail = customerEmail,
        customerPhone = customerPhone,
        customerAddress = customerAddress,
        displayOrderNumber = displayOrderNumber,
        rooms = rooms ?: return null,
        bathrooms = bathrooms ?: return null,
        extras = extras,
        cleaningDateTime = cleaningDateTime?.toString(),
        paymentType = paymentType?.toAppDto() ?: return null,
        paymentStatus = paymentStatus?.toAppDto() ?: return null,
        totalPrice = totalPrice ?: return null,
        originalSubtotal = originalSubtotal ?: return null,
        appliedDiscountSource = appliedDiscountSource?.value ?: return null,
        tierDiscountAmount = tierDiscountAmount,
        membershipDiscountAmount = membershipDiscountAmount,
        promoDiscountAmount = promoDiscountAmount,
        estimatedTime = estimatedTime ?: return null,
        orderStatus = orderStatus?.toAppDto() ?: return null,
        confirmationCode = confirmationCode,
        stripeSessionId = null, // not exposed on generated OrderListItem
        selectedPackages = selectedPackages?.map { it.toListSummary() ?: return null },
        currencyId = currencyId,
        currency = currency?.toAppDto() ?: return null,
        assignedEmployees = assignedEmployees,
        selectedServices = selectedServices?.map { it.toListSummary() ?: return null },
        requiredEmployees = requiredEmployees ?: return null,
        maxEmployees = maxEmployees ?: return null,
        availableSpots = availableSpots ?: return null,
        assignedEmployeesCount = assignedEmployeesCount ?: return null,
        hasAvailableSpots = hasAvailableSpots ?: return null,
    )
}

/**
 * The detail refuses rather than drops: it is one order, and there is no rest of the page to keep.
 * Its `selectedServices` / `selectedPackages` refuse with it — the breakdown rows and the total are
 * read side by side, so a silently shorter breakdown is a total that stops adding up.
 */
private fun GenOrderItem.toAppDto(): OrderDetailDto? {
    return OrderDetailDto(
        id = id ?: return null,
        displayOrderNumber = displayOrderNumber,
        customerName = customerName,
        customerEmail = customerEmail,
        customerPhone = customerPhone,
        address = address?.toAppDto(),
        rooms = rooms ?: return null,
        bathrooms = bathrooms ?: return null,
        extras = extras,
        cleaningDateTime = cleaningDateTime?.toString(),
        paymentType = paymentType?.toAppDto() ?: return null,
        paymentStatus = paymentStatus?.toAppDto() ?: return null,
        totalPrice = totalPrice ?: return null,
        originalSubtotal = originalSubtotal ?: return null,
        appliedDiscountSource = appliedDiscountSource?.value ?: return null,
        tierDiscountAmount = tierDiscountAmount,
        membershipDiscountAmount = membershipDiscountAmount,
        promoDiscountAmount = promoDiscountAmount,
        estimatedTime = estimatedTime ?: return null,
        actualCompletionTime = actualCompletionTime,
        completedAt = completedAt?.toString(),
        completionNotes = completionNotes,
        orderStatus = orderStatus?.toAppDto() ?: return null,
        confirmationCode = confirmationCode,
        stripeSessionId = null, // not exposed on generated OrderItem
        notes = notes,
        specialInstructions = specialInstructions,
        accessInstructions = accessInstructions,
        recurringTemplateId = recurringTemplateId,
        selectedPackages = selectedPackages?.map { it.toAppDto() ?: return null },
        currency = currency?.toAppDto(),
        selectedServices = selectedServices?.map { it.toAppDto() ?: return null },
        statusHistory = statusHistory?.map { it.toAppDto() ?: return null },
        createdOn = createdOn?.toString(),
        updatedOn = updatedOn?.toString(),
        assignedEmployees = assignedEmployees?.map { it.toAppDto() },
        receiptNumber = receiptNumber,
        orderNotes = orderNotes?.map { it.toAppDto() },
        orderIssues = orderIssues?.map { it.toAppDto() ?: return null },
        review = review?.toAppDto() ?: return null,
    )
}

/**
 * `value` is the ordinal every status decision is made from, and `0` is a real status (`New`) rather
 * than an absence — a defaulted code tells the screen the order has not started yet.
 */
private fun GenCode.toAppDto(): CodeDto? {
    return CodeDto(
        type = type.orEmpty(),
        name = name.orEmpty(),
        value = `value` ?: return null,
    )
}

private fun GenOrderAddress.toAppDto(): OrderAddressDto = OrderAddressDto(
    street = street,
    city = city,
    zipCode = zipCode,
    country = country,
    latitude = latitude,
    longitude = longitude,
)

private fun GenOrderStatusTrackDto.toAppDto(): OrderStatusTrackDto? {
    return OrderStatusTrackDto(
        status = status?.toAppDto() ?: return null,
        createdOn = createdOn?.toString(),
    )
}

private fun GenAssignedEmployeeDto.toAppDto(): AssignedEmployeeDto = AssignedEmployeeDto(
    id = id,
    employeeId = employeeId,
    fullName = fullName,
    phoneNumber = phoneNumber,
    email = null, // not on generated DTO
)

private fun GenOrderReviewDto.toAppDto(): OrderReviewDto? {
    return OrderReviewDto(
        id = id,
        orderId = orderId,
        userId = null, // not on generated DTO
        rating = rating ?: return null,
        comment = comment,
        createdOn = createdOn?.toString(),
        updatedOn = updatedOn?.toString(),
    )
}

private fun GenOrderNoteDto.toAppDto(): OrderNoteDto = OrderNoteDto(
    id = id,
    employeeId = employeeId,
    content = content,
    createdOn = createdOn?.toString(),
)

private fun GenOrderIssueDto.toAppDto(): OrderIssueDto? {
    return OrderIssueDto(
        id = id,
        reportedByEmployeeId = reportedByEmployeeId,
        description = description,
        isResolved = isResolved ?: return null,
        resolvedAt = resolvedAt?.toString(),
        createdOn = createdOn?.toString(),
    )
}

private fun GenServiceListItem.toListSummary(): OrderServiceSummaryDto? {
    return OrderServiceSummaryDto(
        id = id,
        name = name,
        description = description,
        basePrice = basePrice ?: return null,
        perRoomPrice = perRoomPrice ?: return null,
    )
}

private fun GenServiceDetails.toAppDto(): OrderServiceDetailsDto? {
    return OrderServiceDetailsDto(
        id = id,
        name = name,
        description = description,
        estimatedTime = estimatedTime ?: return null,
        currencyCode = currencyCode,
    )
}

private fun GenPackageListItem.toListSummary(): OrderPackageSummaryDto? {
    return OrderPackageSummaryDto(
        id = id,
        name = name,
        description = description,
        price = price ?: return null,
    )
}

private fun GenPackageDetails.toAppDto(): OrderPackageDetailsDto? {
    return OrderPackageDetailsDto(
        id = id,
        name = name,
        description = description,
        price = price ?: return null,
        estimatedTime = estimatedTime ?: return null,
        currencyCode = currencyCode,
        includedServices = includedServices,
    )
}

/**
 * A zeroed `exchangeRate` is not a neutral fallback but a claim that every converted figure on the
 * screen is nothing; parity (`1.0`) would be equally invented, off by 24.75× on a CZK order.
 */
private fun GenCurrencyListItem.toAppDto(): OrderCurrencyListItemDto? {
    return OrderCurrencyListItemDto(
        id = id,
        code = code,
        symbol = symbol,
        name = name,
        exchangeRate = exchangeRate ?: return null,
        isDefault = isDefault ?: return null,
    )
}

private fun GenCurrencyDetailDto.toAppDto(): OrderCurrencyDetailDto? {
    return OrderCurrencyDetailDto(
        id = id,
        code = code,
        name = name,
        symbol = symbol,
        exchangeRate = exchangeRate ?: return null,
        isDefault = isDefault ?: return null,
    )
}

/**
 * The receipt for a cancellation that already happened. A defaulted `refundAmount` tells the customer
 * they are getting nothing back on the one screen they will screenshot, and `refundInitiated = false`
 * invents a refund that was never started.
 */
private fun GenCancelOrderResponse.toAppDto(): CancelOrderResponse? {
    return CancelOrderResponse(
        orderId = orderId,
        feeRate = feeRate ?: return null,
        refundAmount = refundAmount ?: return null,
        totalPrice = totalPrice ?: return null,
        refundInitiated = refundInitiated ?: return null,
    )
}

/**
 * Null when the tier is absent — every other field on the generated response is
 * nullable too, so a defaulted tier would quote whatever ordinal 0 happens to
 * mean (a free cancellation) on the strength of a field the server never sent.
 */
private fun GenGetCancellationFeePreviewResponse.toAppDto(): CancellationFeePreviewDto? {
    val tierValue = tier?.`value` ?: return null
    return CancellationFeePreviewDto(
        orderId = orderId,
        tier = tierValue,
        feeRate = feeRate ?: return null,
        feeAmount = feeAmount ?: return null,
        refundAmount = refundAmount ?: return null,
        totalPrice = totalPrice ?: return null,
        currencyCode = currencyCode,
        expressWaiverForfeitedOnCancel = expressWaiverForfeitedOnCancel ?: return null,
    )
}

private fun GenConfirmRecurringOrderResponse.toAppDto(): ConfirmRecurringOrderResponse = ConfirmRecurringOrderResponse(
    orderId = orderId,
    clientSecret = clientSecret,
    paymentIntentId = paymentIntentId,
    stripeCustomerId = stripeCustomerId,
    ephemeralKey = ephemeralKey,
)

private fun GenGetOrderPhotosResponse?.toAppDto(): OrderPhotosResponse? {
    if (this == null) return null
    return OrderPhotosResponse(
        photos = photos.orEmpty().map { it.toAppDto() ?: return null },
        beforePhotoCount = beforePhotoCount ?: return null,
        afterPhotoCount = afterPhotoCount ?: return null,
    )
}

private fun GenOrderPhotoDto.toAppDto(): OrderPhotoDto? {
    return OrderPhotoDto(
        id = id,
        photoType = photoType?.value,
        blobUrl = blobUrl,
        fileName = fileName,
        originalFileName = originalFileName,
        fileSizeBytes = fileSizeBytes ?: return null,
        contentType = contentType,
        capturedAt = capturedAt?.toString(),
        capturedByEmployeeId = capturedByEmployeeId,
        capturedByEmployeeName = capturedByEmployeeName,
        width = width,
        height = height,
        notes = notes,
    )
}

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
