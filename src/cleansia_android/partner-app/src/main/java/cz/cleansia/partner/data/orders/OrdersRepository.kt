package cz.cleansia.partner.data.orders

import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.AddOrderNoteCommand
import cz.cleansia.partner.api.model.BlobFileDto
import cz.cleansia.partner.api.model.CompleteOrderCommand
import cz.cleansia.partner.api.model.GetOrderPhotosResponse
import cz.cleansia.partner.api.model.NotifyOnTheWayCommand
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PagedDataOfOrderListItem
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.partner.api.model.ReportOrderIssueCommand
import cz.cleansia.partner.api.model.SaveOrderPhotosCommand
import cz.cleansia.partner.api.model.SaveOrderPhotosPhotoToSave
import cz.cleansia.partner.api.model.SortDefinition
import cz.cleansia.partner.api.model.SortDirection
import cz.cleansia.partner.api.model.StartOrderCommand
import cz.cleansia.partner.api.model.TakeOrderCommand
import cz.cleansia.partner.api.model.UpdateOrderIssueCommand
import cz.cleansia.partner.api.model.UpdateOrderNoteCommand
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import kotlinx.serialization.json.Json
import java.util.concurrent.ConcurrentHashMap
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Logical pane on the Orders list. The repo holds an independent
 * [Staleness] watermark per pane so the VM can ask "is Active stale?"
 * without the repo having to remember which pane each call belongs to.
 * Independence matters because a mutation usually only invalidates a
 * subset (e.g. completing an order should expire Active + History but
 * leave Available alone), and the user pulling on one pane shouldn't
 * trigger background refreshes on the other two.
 */
enum class OrdersPane { Available, Active, History }

/**
 * Orders contract used by every Orders feature ViewModel. Mirrors the
 * partner-mobile-api `OrderController` shape (paged list, get-by-id,
 * lifecycle transitions, notes, photos, issue reports).
 */
interface OrdersRepository {
    suspend fun getPaged(
        statuses: List<OrderStatus>? = null,
        isUnassigned: Boolean? = null,
        employeeId: String? = null,
        cleaningDateFrom: String? = null,
        cleaningDateTo: String? = null,
        sortField: String = "cleaningDateTime",
        sortAscending: Boolean = true,
        offset: Int = 0,
        limit: Int = 50,
        /**
         * Logical pane this fetch belongs to. On success the repo stamps
         * the matching pane's [Staleness] watermark so future freshness
         * checks for the same pane can short-circuit the network. `null`
         * for ad-hoc internal callers that don't participate in the
         * silent-stale flow.
         */
        pane: OrdersPane? = null,
    ): ApiResult<PagedDataOfOrderListItem>

    suspend fun getById(orderId: String): ApiResult<OrderItem>

    suspend fun takeOrder(orderId: String): ApiResult<Unit>
    suspend fun startOrder(orderId: String): ApiResult<Unit>
    suspend fun notifyOnTheWay(orderId: String): ApiResult<Unit>
    suspend fun completeOrder(
        orderId: String,
        actualCompletionMinutes: Int?,
        completionNotes: String?,
    ): ApiResult<Unit>

    suspend fun getPhotos(orderId: String): ApiResult<GetOrderPhotosResponse>

    /** Uploads a single photo via the batch-of-one endpoint (matches customer pattern). */
    suspend fun uploadPhoto(
        orderId: String,
        photoType: PhotoType,
        fileName: String,
        contentType: String,
        base64Content: String,
        notes: String? = null,
    ): ApiResult<Unit>

    suspend fun deletePhoto(photoId: String): ApiResult<Unit>

    suspend fun addNote(orderId: String, content: String): ApiResult<Unit>
    suspend fun updateNote(orderId: String, noteId: String, content: String): ApiResult<Unit>
    suspend fun deleteNote(orderId: String, noteId: String): ApiResult<Unit>

    suspend fun reportIssue(orderId: String, content: String): ApiResult<Unit>
    suspend fun updateIssue(orderId: String, issueId: String, description: String): ApiResult<Unit>
    suspend fun deleteIssue(orderId: String, issueId: String): ApiResult<Unit>

    /**
     * `true` if the cached single-order payload for [orderId] is stale and a
     * fresh fetch should be issued. ViewModels use this on screen entry /
     * resume to gate background refreshes: skip the network when the cache
     * is still warm (default 30s window). User-initiated pulls bypass this.
     */
    fun isOrderStale(orderId: String): Boolean

    /**
     * Drop the staleness watermark for [orderId], forcing the next
     * freshness check to fall through to a network fetch. Called after
     * any mutation (take/start/notifyOnTheWay/complete, photo upload /
     * delete, note / issue add / update / delete) so the detail view
     * refetches the canonical server state.
     */
    fun invalidateOrder(orderId: String)

    /**
     * `true` if the cached list payload for [pane] is stale. The Orders
     * list VM calls this on screen entry / tab switch / ON_RESUME to
     * decide whether to silently refetch — fresh => skip the network and
     * keep whatever's already on screen.
     */
    fun isPaneStale(pane: OrdersPane): Boolean

    /**
     * Reset the watermark for every pane impacted by the given mutation
     * (see [OrdersMutation]). Called from the list VM immediately after
     * a successful inline action, before triggering the silent re-fetch,
     * so the affected panes are guaranteed to refetch on the next
     * `ensureFresh` call.
     */
    fun invalidatePanesFor(mutation: OrdersMutation)
}

/**
 * Lifecycle mutation tag used to decide which list panes to invalidate.
 * Each value maps to the *visible consequence* of the mutation:
 *  - [TakeOrder] / [NotifyOnTheWay]: order leaves Available, enters / stays in Active.
 *  - [StartOrder]: stays in Active but the status pill flips — refresh Active.
 *  - [CompleteOrder]: leaves Active, enters History.
 */
enum class OrdersMutation { TakeOrder, NotifyOnTheWay, StartOrder, CompleteOrder }

@Singleton
class OrdersRepositoryImpl @Inject constructor(
    private val orderApi: OrderApi,
    private val json: Json,
) : OrdersRepository {

    // Per-order freshness watermarks. A separate Staleness instance per
    // orderId means a mutation on order A doesn't force order B's detail
    // screen to refetch. computeIfAbsent keeps lookups lock-free on the
    // hot read path while still being safe under concurrent fetches.
    private val orderStaleness = ConcurrentHashMap<String, Staleness>()

    // Per-pane list watermarks. Three independent instances so a pull on
    // Available doesn't make Active/History look stale, and a mutation
    // that only affects two panes leaves the third's cache warm.
    private val paneStaleness: Map<OrdersPane, Staleness> = mapOf(
        OrdersPane.Available to Staleness(),
        OrdersPane.Active to Staleness(),
        OrdersPane.History to Staleness(),
    )

    private fun stalenessFor(orderId: String): Staleness =
        orderStaleness.getOrPut(orderId) { Staleness() }

    private fun stalenessFor(pane: OrdersPane): Staleness =
        paneStaleness.getValue(pane)

    override fun isOrderStale(orderId: String): Boolean =
        stalenessFor(orderId).isStale()

    override fun invalidateOrder(orderId: String) {
        stalenessFor(orderId).reset()
    }

    override fun isPaneStale(pane: OrdersPane): Boolean =
        stalenessFor(pane).isStale()

    override fun invalidatePanesFor(mutation: OrdersMutation) {
        // Map each lifecycle mutation to the panes whose displayed data
        // it could change. The third pane is left alone so its cached
        // list keeps serving silently on the next ensureFresh.
        val affected = when (mutation) {
            OrdersMutation.TakeOrder,
            OrdersMutation.NotifyOnTheWay -> listOf(OrdersPane.Available, OrdersPane.Active)
            OrdersMutation.StartOrder -> listOf(OrdersPane.Active)
            OrdersMutation.CompleteOrder -> listOf(OrdersPane.Active, OrdersPane.History)
        }
        affected.forEach { stalenessFor(it).reset() }
    }

    override suspend fun getPaged(
        statuses: List<OrderStatus>?,
        isUnassigned: Boolean?,
        employeeId: String?,
        cleaningDateFrom: String?,
        cleaningDateTo: String?,
        sortField: String,
        sortAscending: Boolean,
        offset: Int,
        limit: Int,
        pane: OrdersPane?,
    ): ApiResult<PagedDataOfOrderListItem> = safeApiCall(json) {
        orderApi.orderGetPaged(
            filterOrderStatuses = statuses,
            filterIsUnassigned = isUnassigned,
            filterEmployeeId = employeeId,
            filterCleaningDateFrom = cleaningDateFrom,
            filterCleaningDateTo = cleaningDateTo,
            sort = listOf(SortDefinition(
                field = sortField,
                direction = if (sortAscending) SortDirection._0 else SortDirection._1,
            )),
            offset = offset,
            limit = limit,
        )
    }.also { result ->
        // Stamp the pane watermark only on success — a transient API
        // failure shouldn't pretend we have a fresh cache. `pane == null`
        // skips the stamp so internal/ad-hoc callers don't accidentally
        // suppress the next refresh.
        if (pane != null && result is ApiResult.Success) stalenessFor(pane).markFresh()
    }

    override suspend fun getById(orderId: String): ApiResult<OrderItem> =
        safeApiCall(json) { orderApi.orderGetById(orderId) }
            .also { result ->
                // Stamp the watermark only on success so a transient API
                // failure doesn't pretend we have a fresh cache.
                if (result is ApiResult.Success) stalenessFor(orderId).markFresh()
            }

    override suspend fun takeOrder(orderId: String): ApiResult<Unit> = safeApiCall(json) {
        orderApi.orderTakeOrder(TakeOrderCommand(orderId = orderId))
    }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun startOrder(orderId: String): ApiResult<Unit> = safeApiCall(json) {
        orderApi.orderStartOrder(StartOrderCommand(orderId = orderId))
    }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun notifyOnTheWay(orderId: String): ApiResult<Unit> = safeApiCall(json) {
        orderApi.orderNotifyOnTheWay(NotifyOnTheWayCommand(orderId = orderId))
    }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun completeOrder(
        orderId: String,
        actualCompletionMinutes: Int?,
        completionNotes: String?,
    ): ApiResult<Unit> = safeApiCall(json) {
        orderApi.orderCompleteOrder(
            CompleteOrderCommand(
                orderId = orderId,
                actualCompletionTimeMinutes = actualCompletionMinutes,
                completionNotes = completionNotes,
            ),
        )
    }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun getPhotos(orderId: String): ApiResult<GetOrderPhotosResponse> =
        safeApiCall(json) { orderApi.orderGetPhotos(orderId) }

    override suspend fun uploadPhoto(
        orderId: String,
        photoType: PhotoType,
        fileName: String,
        contentType: String,
        base64Content: String,
        notes: String?,
    ): ApiResult<Unit> = safeApiCall(json) {
        // Single-photo upload via SavePhotos (base64 batch). The byte[]-based
        // UploadPhoto endpoint exists too but base64 keeps the network module
        // simple — no multipart wiring required.
        orderApi.orderSavePhotos(
            SaveOrderPhotosCommand(
                orderId = orderId,
                photos = listOf(
                    SaveOrderPhotosPhotoToSave(
                        photoType = photoType,
                        file = BlobFileDto(
                            fileName = fileName,
                            contentType = contentType,
                            base64Content = base64Content,
                        ),
                        notes = notes,
                    ),
                ),
            ),
        )
    }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    // deletePhoto's API contract only carries a photoId — we don't know
    // which order owns it here. The detail VM compensates by invalidating
    // its own orderId immediately after a successful delete.
    override suspend fun deletePhoto(photoId: String): ApiResult<Unit> =
        safeApiCall(json) { orderApi.orderDeletePhoto(photoId) }.map { }

    override suspend fun addNote(orderId: String, content: String): ApiResult<Unit> =
        safeApiCall(json) {
            orderApi.orderAddNote(AddOrderNoteCommand(orderId = orderId, content = content))
        }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun reportIssue(orderId: String, content: String): ApiResult<Unit> =
        safeApiCall(json) {
            orderApi.orderReportIssue(ReportOrderIssueCommand(orderId = orderId, description = content))
        }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun updateNote(orderId: String, noteId: String, content: String): ApiResult<Unit> =
        safeApiCall(json) {
            orderApi.orderUpdateNote(
                UpdateOrderNoteCommand(orderId = orderId, noteId = noteId, content = content),
            )
        }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun deleteNote(orderId: String, noteId: String): ApiResult<Unit> =
        safeApiCall(json) {
            orderApi.orderDeleteNote(orderId = orderId, noteId = noteId)
        }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun updateIssue(orderId: String, issueId: String, description: String): ApiResult<Unit> =
        safeApiCall(json) {
            orderApi.orderUpdateIssue(
                UpdateOrderIssueCommand(orderId = orderId, issueId = issueId, description = description),
            )
        }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }

    override suspend fun deleteIssue(orderId: String, issueId: String): ApiResult<Unit> =
        safeApiCall(json) {
            orderApi.orderDeleteIssue(orderId = orderId, issueId = issueId)
        }.map { }.also { if (it is ApiResult.Success) invalidateOrder(orderId) }
}
