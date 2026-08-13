package cz.cleansia.customer.core.orders

import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.AssignedEmployeeDto as GenAssignedEmployeeDto
import cz.cleansia.customer.api.model.CancelOrderCommand as GenCancelOrderCommand
import cz.cleansia.customer.api.model.CancelOrderResponse as GenCancelOrderResponse
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
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import cz.cleansia.customer.core.user.toAppDto
import okhttp3.ResponseBody
import retrofit2.Response

/**
 * Adapter over the generated order API.
 *
 * **The hand-written DTOs carry stable shapes** — defaulted primitives rather than the generated
 * all-nullable wire types — so screens and ViewModels do not deal with nullability the server never
 * actually sends. -> /mobile-app/api-integration
 */
class OrderApi(
    private val orderApi: GenOrderApi,
) {
    suspend fun getMyOrders(offset: Int = 0, limit: Int = 20): Response<OrderListResponseDto> {
        val raw = orderApi.orderGetMyOrders(offset = offset, limit = limit)
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun getById(id: String): Response<OrderDetailDto> {
        val raw = orderApi.orderGetById(orderId = id)
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun cancel(body: CancelOrderRequest): Response<CancelOrderResponse> {
        val raw = orderApi.orderCancelOrder(
            cancelOrderCommand = GenCancelOrderCommand(orderId = body.orderId, reason = body.reason),
        )
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun getCancellationPreview(id: String): Response<CancellationFeePreviewDto> {
        val raw = orderApi.orderCancellationPreview(orderId = id)
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun confirmRecurring(body: ConfirmRecurringOrderRequest): Response<ConfirmRecurringOrderResponse> {
        val raw = orderApi.orderConfirmRecurring(
            confirmRecurringOrderCommand = GenConfirmRecurringOrderCommand(orderId = body.orderId),
        )
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun submitReview(body: SubmitReviewRequest): Response<OrderReviewDto> {
        val raw = orderApi.orderSubmitReview(
            submitOrderReviewCommand = GenSubmitOrderReviewCommand(
                orderId = body.orderId,
                rating = body.rating,
                comment = body.comment,
            ),
        )
        return raw.mapWire { it.toAppDto() }
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
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun getMyServingCleaners(): Response<List<ServingCleanerDto>> {
        val raw = orderApi.orderMyServingCleaners()
        return raw.mapWire { list -> list.orEmpty().mapNotNull { it.toAppDtoOrDrop() } }
    }
}

// ─── Generated → app DTO mappers ───

/**
 * `total` drives "load more": a defaulted zero silently ends pagination, so the customer's older
 * orders stop existing rather than fail to load. A null wrapper is a 200 with no page in it and is
 * refused for the same reason.
 */
private fun GenPagedDataOfOrderListItem?.toAppDto(): OrderListResponseDto {
    val page = required("PagedDataOfOrderListItem")
    return OrderListResponseDto(
        pageNumber = page.pageNumber.required("pageNumber"),
        pageSize = page.pageSize.required("pageSize"),
        total = page.total.required("total"),
        // The two rulings, each where it is decided: an unidentifiable row is dropped, a row whose own
        // money is broken refuses the page.
        receivedCount = page.`data`.orEmpty().size,
        data = page.`data`.orEmpty()
            .filter { it.id != null }
            .map { it.toAppDtoOrRefuse() },
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
private fun GenOrderListItem.toAppDtoOrRefuse(): OrderListItemDto = OrderListItemDto(
    id = id.required("id"),
    customerName = customerName,
    customerEmail = customerEmail,
    customerPhone = customerPhone,
    customerAddress = customerAddress,
    displayOrderNumber = displayOrderNumber,
    rooms = rooms.required("rooms"),
    bathrooms = bathrooms.required("bathrooms"),
    extras = extras,
    cleaningDateTime = cleaningDateTime?.toString(),
    paymentType = paymentType?.toAppDto().required("paymentType"),
    paymentStatus = paymentStatus?.toAppDto().required("paymentStatus"),
    totalPrice = totalPrice.required("totalPrice"),
    originalSubtotal = originalSubtotal.required("originalSubtotal"),
    appliedDiscountSource = appliedDiscountSource?.`value`.required("appliedDiscountSource"),
    tierDiscountAmount = tierDiscountAmount,
    membershipDiscountAmount = membershipDiscountAmount,
    promoDiscountAmount = promoDiscountAmount,
    estimatedTime = estimatedTime.required("estimatedTime"),
    orderStatus = orderStatus?.toAppDto().required("orderStatus"),
    confirmationCode = confirmationCode,
    stripeSessionId = null, // not exposed on generated OrderListItem
    selectedPackages = selectedPackages?.map { it.toListSummary() },
    currencyId = currencyId,
    currency = currency?.toAppDto().required("currency"),
    assignedEmployees = assignedEmployees,
    selectedServices = selectedServices?.map { it.toListSummary() },
    requiredEmployees = requiredEmployees.required("requiredEmployees"),
    maxEmployees = maxEmployees.required("maxEmployees"),
    availableSpots = availableSpots.required("availableSpots"),
    assignedEmployeesCount = assignedEmployeesCount.required("assignedEmployeesCount"),
    hasAvailableSpots = hasAvailableSpots.required("hasAvailableSpots"),
)

/**
 * The detail refuses rather than drops: it is one order, and there is no rest of the page to keep.
 * Its `selectedServices` / `selectedPackages` refuse with it — the breakdown rows and the total are
 * read side by side, so a silently shorter breakdown is a total that stops adding up.
 */
private fun GenOrderItem?.toAppDto(): OrderDetailDto {
    val order = required("OrderItem")
    return OrderDetailDto(
        id = order.id.required("id"),
        displayOrderNumber = order.displayOrderNumber,
        customerName = order.customerName,
        customerEmail = order.customerEmail,
        customerPhone = order.customerPhone,
        address = order.address?.toAppDto(),
        rooms = order.rooms.required("rooms"),
        bathrooms = order.bathrooms.required("bathrooms"),
        extras = order.extras,
        cleaningDateTime = order.cleaningDateTime?.toString(),
        paymentType = order.paymentType?.toAppDto().required("paymentType"),
        paymentStatus = order.paymentStatus?.toAppDto().required("paymentStatus"),
        totalPrice = order.totalPrice.required("totalPrice"),
        originalSubtotal = order.originalSubtotal.required("originalSubtotal"),
        appliedDiscountSource = order.appliedDiscountSource?.`value`.required("appliedDiscountSource"),
        tierDiscountAmount = order.tierDiscountAmount,
        membershipDiscountAmount = order.membershipDiscountAmount,
        promoDiscountAmount = order.promoDiscountAmount,
        estimatedTime = order.estimatedTime.required("estimatedTime"),
        actualCompletionTime = order.actualCompletionTime,
        completedAt = order.completedAt?.toString(),
        completionNotes = order.completionNotes,
        orderStatus = order.orderStatus?.toAppDto().required("orderStatus"),
        confirmationCode = order.confirmationCode,
        stripeSessionId = null, // not exposed on generated OrderItem
        notes = order.notes,
        specialInstructions = order.specialInstructions,
        accessInstructions = order.accessInstructions,
        recurringTemplateId = order.recurringTemplateId,
        selectedPackages = order.selectedPackages?.map { it.toAppDto() },
        currency = order.currency?.toAppDto(),
        selectedServices = order.selectedServices?.map { it.toAppDto() },
        statusHistory = order.statusHistory?.map { it.toAppDto() },
        createdOn = order.createdOn?.toString(),
        updatedOn = order.updatedOn?.toString(),
        assignedEmployees = order.assignedEmployees?.map { it.toAppDto() },
        receiptNumber = order.receiptNumber,
        orderNotes = order.orderNotes?.map { it.toAppDto() },
        orderIssues = order.orderIssues?.map { it.toAppDto() },
        review = order.review?.toAppDto(),
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

private fun GenOrderStatusTrackDto.toAppDto(): OrderStatusTrackDto = OrderStatusTrackDto(
    status = status?.toAppDto().required("status"),
    createdOn = createdOn?.toString(),
)

private fun GenAssignedEmployeeDto.toAppDto(): AssignedEmployeeDto = AssignedEmployeeDto(
    id = id,
    employeeId = employeeId,
    fullName = fullName,
    phoneNumber = phoneNumber,
    email = null, // not on generated DTO
)

private fun GenOrderReviewDto?.toAppDto(): OrderReviewDto {
    val review = required("OrderReviewDto")
    return OrderReviewDto(
        id = review.id,
        orderId = review.orderId,
        userId = null, // not on generated DTO
        rating = review.rating.required("rating"),
        comment = review.comment,
        createdOn = review.createdOn?.toString(),
        updatedOn = review.updatedOn?.toString(),
    )
}

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
    isResolved = isResolved.required("isResolved"),
    resolvedAt = resolvedAt?.toString(),
    createdOn = createdOn?.toString(),
)

private fun GenServiceListItem.toListSummary(): OrderServiceSummaryDto = OrderServiceSummaryDto(
    id = id,
    name = name,
    description = description,
    basePrice = basePrice.required("basePrice"),
    perRoomPrice = perRoomPrice.required("perRoomPrice"),
)

private fun GenServiceDetails.toAppDto(): OrderServiceDetailsDto = OrderServiceDetailsDto(
    id = id,
    name = name,
    description = description,
    estimatedTime = estimatedTime.required("estimatedTime"),
    currencyCode = currencyCode,
)

private fun GenPackageListItem.toListSummary(): OrderPackageSummaryDto = OrderPackageSummaryDto(
    id = id,
    name = name,
    description = description,
    price = price.required("price"),
)

private fun GenPackageDetails.toAppDto(): OrderPackageDetailsDto = OrderPackageDetailsDto(
    id = id,
    name = name,
    description = description,
    price = price.required("price"),
    estimatedTime = estimatedTime.required("estimatedTime"),
    currencyCode = currencyCode,
    includedServices = includedServices,
)

/**
 * A zeroed `exchangeRate` is not a neutral fallback but a claim that every converted figure on the
 * screen is nothing; parity (`1.0`) would be equally invented, off by 24.75× on a CZK order.
 */
private fun GenCurrencyListItem.toAppDto(): OrderCurrencyListItemDto = OrderCurrencyListItemDto(
    id = id,
    code = code,
    symbol = symbol,
    name = name,
    exchangeRate = exchangeRate.required("exchangeRate"),
    isDefault = isDefault.required("isDefault"),
)

private fun GenCurrencyDetailDto.toAppDto(): OrderCurrencyDetailDto = OrderCurrencyDetailDto(
    id = id,
    code = code,
    name = name,
    symbol = symbol,
    exchangeRate = exchangeRate.required("exchangeRate"),
    isDefault = isDefault.required("isDefault"),
)

/**
 * The receipt for a cancellation that already happened. A defaulted `refundAmount` tells the customer
 * they are getting nothing back on the one screen they will screenshot, and `refundInitiated = false`
 * invents a refund that was never started.
 */
private fun GenCancelOrderResponse?.toAppDto(): CancelOrderResponse {
    val receipt = required("CancelOrderResponse")
    return CancelOrderResponse(
        orderId = receipt.orderId,
        feeRate = receipt.feeRate.required("feeRate"),
        refundAmount = receipt.refundAmount.required("refundAmount"),
        totalPrice = receipt.totalPrice.required("totalPrice"),
        refundInitiated = receipt.refundInitiated.required("refundInitiated"),
    )
}

/**
 * The tier is refused rather than defaulted — every other field on the generated response is nullable
 * too, so ordinal 0 would quote a free cancellation on the strength of a field the server never sent.
 */
private fun GenGetCancellationFeePreviewResponse?.toAppDto(): CancellationFeePreviewDto {
    val quote = required("GetCancellationFeePreviewResponse")
    return CancellationFeePreviewDto(
        orderId = quote.orderId,
        tier = quote.tier?.`value`.required("tier"),
        feeRate = quote.feeRate.required("feeRate"),
        feeAmount = quote.feeAmount.required("feeAmount"),
        refundAmount = quote.refundAmount.required("refundAmount"),
        totalPrice = quote.totalPrice.required("totalPrice"),
        currencyCode = quote.currencyCode,
        expressWaiverForfeitedOnCancel =
            quote.expressWaiverForfeitedOnCancel.required("expressWaiverForfeitedOnCancel"),
    )
}

private fun GenConfirmRecurringOrderResponse?.toAppDto(): ConfirmRecurringOrderResponse {
    val confirmation = required("ConfirmRecurringOrderResponse")
    return ConfirmRecurringOrderResponse(
        orderId = confirmation.orderId,
        clientSecret = confirmation.clientSecret,
        paymentIntentId = confirmation.paymentIntentId,
        stripeCustomerId = confirmation.stripeCustomerId,
        ephemeralKey = confirmation.ephemeralKey,
    )
}

private fun GenGetOrderPhotosResponse?.toAppDto(): OrderPhotosResponse {
    val photos = required("GetOrderPhotosResponse")
    return OrderPhotosResponse(
        photos = photos.photos.orEmpty().map { it.toAppDto() },
        beforePhotoCount = photos.beforePhotoCount.required("beforePhotoCount"),
        afterPhotoCount = photos.afterPhotoCount.required("afterPhotoCount"),
    )
}

private fun GenOrderPhotoDto.toAppDto(): OrderPhotoDto = OrderPhotoDto(
    id = id,
    photoType = photoType?.`value`,
    blobUrl = blobUrl,
    fileName = fileName,
    originalFileName = originalFileName,
    fileSizeBytes = fileSizeBytes.required("fileSizeBytes"),
    contentType = contentType,
    capturedAt = capturedAt?.toString(),
    capturedByEmployeeId = capturedByEmployeeId,
    capturedByEmployeeName = capturedByEmployeeName,
    width = width,
    height = height,
    notes = notes,
)

/**
 * Drops rather than refuses, and keeps doing so: nothing sums this list — it is the favourite-cleaner
 * picker, and its failure mode is already "no preference, the server matches normally". A refusal
 * would empty the picker outright, which is the same screen with one more round-trip lost.
 */
private fun GenGetMyServingCleanersResponse.toAppDtoOrDrop(): ServingCleanerDto? {
    return ServingCleanerDto(
        employeeId = employeeId ?: return null,
        fullName = fullName ?: return null,
        lastServedOn = lastServedOn?.toString() ?: return null,
    )
}
