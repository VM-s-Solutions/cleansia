# Orders Integration — Wave 2 (customer Android)

Wave 1 shipped the read path (list, detail, home recent, booking success). Wave 2 wires the five order actions: cancel, review, report issue, download receipt, view photos.

## Backend status (audited 2026-04-24)

| Action | Endpoint | Status |
|---|---|---|
| Cancel order | `POST /api/Order/Cancel` (Customer API) | Full support, 24h policy + Stripe refund |
| Submit review | `POST /api/Order/SubmitReview` | Full support, 1–5 stars + comment, Completed-only |
| Download receipt | `GET /api/Order/DownloadReceipt?OrderId=…` | Full support, returns PDF bytes (`FileContentResult`) |
| View photos | `GET /api/Order/GetPhotos?OrderId=…` | Full support, SAS-signed `BlobUrl` (1h TTL), single array with Before/After discriminator |
| Report issue | `POST /api/Dispute/Create` (Customer API) | Full Disputes feature — use it. `OrderIssue.ReportOrderIssue` is employee-only by design (authz gate: employee must be assigned to order). Customer issues = Disputes. |

**No backend work needed.** All endpoints exist on the Customer API (port 5003), same surface the mobile app already uses for Wave 1.

## Decision: Report issue routes to Disputes

`OrderIssue` is cleaner-authored ("field notes, I found pet hair"). `Dispute` is customer-authored ("I'm unhappy with the cleaning"). Web customer app already takes this split — `orderDetail.reportIssue()` navigates to `/disputes?orderId=…`. Mobile mirrors that: the "Report issue" action on order detail opens a new mobile Disputes screen pre-filled with the order id.

## Scope

Six phases:

1. **Data layer extensions** — extend `OrderApi` + `OrderRepository` with cancel/review/photos/receipt methods. New `DisputeApi` + `DisputeRepository` for the disputes feature. New DTOs.
2. **Cancel order** — bottom sheet with policy-aware fee preview + confirmation. Updates OrderDetail footer.
3. **Submit review** — bottom sheet, stars + comment, Completed-only gate. Replaces "Coming soon" placeholder.
4. **Download receipt** — fetch bytes, save to cache, launch system share/view intent (PDF viewer). Replaces disabled button.
5. **Photo gallery** — new screen with Before/After tabs, thumb grid → fullscreen pager with Coil.
6. **Disputes** — new mobile Disputes list + create + detail (messaging). "Report issue" CTA on OrderDetail deep-links here with orderId pre-fill.

Phases 2–5 can run in parallel after phase 1. Phase 6 is the biggest and can also run after phase 1 — it's a self-contained feature.

---

## Phase 1 — Data layer extensions

### New DTOs (`core/orders/OrderDtos.kt`)

```kotlin
// Cancel
@Serializable
data class CancelOrderRequest(
    val orderId: String,
    val reason: String? = null,
)

@Serializable
data class CancelOrderResponse(
    val feeRate: Double = 0.0,          // 0.0 / 0.5 / 1.0
    val refundAmount: Double = 0.0,
    val refundInitiated: Boolean = false,
)

// Review
@Serializable
data class SubmitReviewRequest(
    val orderId: String,
    val rating: Int,
    val comment: String? = null,
)

// Photos — new file core/orders/OrderPhotoDtos.kt (or inline)
@Serializable
data class OrderPhotosResponse(
    val photos: List<OrderPhotoDto> = emptyList(),
    val beforePhotoCount: Int = 0,
    val afterPhotoCount: Int = 0,
)

@Serializable
data class OrderPhotoDto(
    val id: String? = null,
    val photoType: String? = null,   // "Before" | "After" (enum string from backend)
    val blobUrl: String? = null,      // FULLY SIGNED — pass to Coil as-is
    val fileName: String? = null,
    val contentType: String? = null,
    val fileSizeBytes: Long = 0L,
    val capturedAt: String? = null,   // ISO-8601
    val capturedByEmployeeId: String? = null,
    val capturedByEmployeeName: String? = null,
    val width: Int? = null,
    val height: Int? = null,
    val notes: String? = null,
)
```

### Extend `OrderApi.kt`

```kotlin
@POST("api/Order/Cancel")
suspend fun cancel(@Body body: CancelOrderRequest): Response<CancelOrderResponse>

@POST("api/Order/SubmitReview")
suspend fun submitReview(@Body body: SubmitReviewRequest): Response<OrderReviewDto>

@GET("api/Order/DownloadReceipt")
@Streaming
suspend fun downloadReceipt(@Query("OrderId") id: String): Response<okhttp3.ResponseBody>

@GET("api/Order/GetPhotos")
suspend fun getPhotos(@Query("OrderId") id: String): Response<OrderPhotosResponse>
```

`@Streaming` is important for the PDF — avoids buffering to memory twice. The byte handling happens in the repo.

### Extend `OrderRepository.kt`

```kotlin
suspend fun cancel(orderId: String, reason: String?): BusinessResult<CancelOrderResponse>
suspend fun submitReview(orderId: String, rating: Int, comment: String?): BusinessResult<OrderReviewDto>
suspend fun downloadReceipt(orderId: String): BusinessResult<File>   // saved to cache, returns File
suspend fun getPhotos(orderId: String): BusinessResult<OrderPhotosResponse>
```

After `cancel` and `submitReview` succeed, invalidate the cached detail so the next view refetches. Simplest: don't cache details in the repo; the OrderDetailViewModel re-fetches. If Wave 1 caches in-repo, tell the VM to refresh.

Use a `BusinessResult<T>` sealed type (`Success(value) | Failure(messageResId: Int | message: String)`) so VMs can branch cleanly. If Wave 1 doesn't have this type yet, add it — the current "return `String?` error" pattern is fine for basic flows but doesn't carry typed payloads. Alternatively, return pairs/sealed interfaces per-method; pick whichever is consistent with Wave 1.

### New files for disputes

- `core/disputes/DisputeDtos.kt`
- `core/disputes/DisputeApi.kt` with:
  - `GET /api/Dispute/GetMy` (paged)
  - `GET /api/Dispute/GetById?DisputeId=…`
  - `POST /api/Dispute/Create`
  - `POST /api/Dispute/AddMessage`
- `core/disputes/DisputeRepository.kt` — mirrors `OrderRepository` shape: `disputes`, `totalRecords`, `loading`, `loaded`, `refresh()`, `loadNextPage()`, `getById(id)`, `create(request)`, `addMessage(disputeId, content)`, `clear()`.
- `core/disputes/DisputeModule.kt` — Hilt provides API via `@AuthRetrofit`.
- `core/disputes/DisputeRepositoryEntryPoint.kt` — for non-VM composable access.
- Sign-out plumbing: `DisputeRepository.clear()` call-sites alongside `OrderRepository.clear()` in `AuthAuthenticator` (both branches), `AuthRepository.logout`, and `UserRepository.deleteAccount`.

**Verification**: DI cycle check — `DisputeRepository` ends up in the auth graph if any auth component depends on it. Use `javax.inject.Provider<DisputeRepository>` if needed (same pattern Wave 1 used for `OrderRepository`).

**Files touched**: 5 new in `core/disputes/`, 2 extended (`OrderApi.kt`, `OrderRepository.kt`, `OrderDtos.kt`), 3 touched for sign-out wiring. Estimated ~250 lines net.

---

## Phase 2 — Cancel order

### UX

Footer action on `OrderDetailScreen` (currently Wave 1 leaves footer empty): **only** show "Cancel order" when `orderStatus in {New, Pending, Confirmed}`. Hide for InProgress/Completed/Cancelled.

Tap → opens `CancelOrderSheet` (`ModalBottomSheet`):

**Sheet contents**:
1. Title: "Cancel this cleaning?"
2. Fee preview — depends on `cleaningDateTime` relative to `now()`:
   - ≥24h out → "Free cancellation"
   - 4–24h out → "50% cancellation fee — {refundAmount}"
   - <4h out → "Cancellation fee is 100% — no refund"
   - Oops window (within 15min of booking, 60min for first-time customers) → "Free cancellation"
   - **IMPORTANT**: Compute preview client-side for UX, but the backend response is authoritative. Show the preview, confirm, then show actual fee from response if it differs.
3. Reason field — optional multi-line text (max 2000 chars, match backend constraint).
4. Primary button: "Confirm cancellation" (red/destructive tint).
5. Secondary button: "Keep my booking".

**Submit flow**:
- Loading state on primary button.
- On success: dismiss sheet, refetch order detail (VM's `refresh()`), show snackbar with refund info ("Refund of {amount} initiated" / "Cancellation confirmed, no refund").
- On failure: show error inside sheet, don't dismiss.

### Client-side fee preview helper

New file `core/booking/CancellationPolicy.kt`:
```kotlin
data class CancellationPreview(
    val feeRatePercent: Int,   // 0, 50, 100
    val isOopsWindow: Boolean,
    val hoursUntilStart: Double,
)

fun computeCancellationPreview(
    cleaningDateTime: Instant,
    bookedAt: Instant,
    now: Instant = Clock.System.now(),
    isFirstTimeCustomer: Boolean = false,
): CancellationPreview {
    val oopsWindowMinutes = if (isFirstTimeCustomer) 60L else 15L
    val minutesSinceBooking = (now - bookedAt).inWholeMinutes
    if (minutesSinceBooking <= oopsWindowMinutes) {
        return CancellationPreview(0, isOopsWindow = true, (cleaningDateTime - now).inWholeHours.toDouble())
    }
    val hoursUntil = (cleaningDateTime - now).inWholeMinutes / 60.0
    val rate = when {
        hoursUntil >= 24.0 -> 0
        hoursUntil >= 4.0 -> 50
        else -> 100
    }
    return CancellationPreview(rate, isOopsWindow = false, hoursUntil)
}
```

We don't know `bookedAt` from `OrderDetailDto.createdOn` — actually we do (`createdOn` is the creation timestamp). Use it. Check for null.

`isFirstTimeCustomer` — unknown client-side. Safer to omit this flag from preview logic and just show the standard 15-min oops window. Backend still honors it authoritatively.

### Files

- `features/orders/CancelOrderSheet.kt` — new file, the sheet composable.
- `features/orders/OrderDetailScreen.kt` — add footer action bar (bottom-aligned, sticky), show/hide based on status, open sheet on click.
- `features/orders/OrderDetailViewModel.kt` — add `cancel(reason: String?): Flow<CancelResult>` method. Refetch order on success.
- `core/booking/CancellationPolicy.kt` — new file.

### i18n (new keys, EN + CS)

- `order_cancel_title`, `order_cancel_fee_free`, `order_cancel_fee_50`, `order_cancel_fee_100`, `order_cancel_fee_oops`
- `order_cancel_reason_label`, `order_cancel_reason_placeholder`
- `order_cancel_confirm`, `order_cancel_keep`
- `order_cancel_success_with_refund`, `order_cancel_success_no_refund`
- `order_action_cancel` (footer button)

---

## Phase 3 — Submit review

### UX

On OrderDetail, the Wave 1 "Coming soon" review placeholder (shown when `orderStatus == Completed && review == null`) becomes an active CTA: "Leave a review". Tap → `SubmitReviewSheet` (`ModalBottomSheet`).

**Sheet contents**:
1. Title: "Rate your cleaning".
2. Star row: 5 tappable stars (filled → amber tint, outline → grey). Default 0; submit disabled until rating ≥ 1.
3. Optional comment textarea, max 2000 chars, placeholder "Tell us more…".
4. Primary button: "Submit review" (disabled while rating == 0).

**Submit flow**:
- On success: refetch order, dismiss, snackbar "Thanks — review submitted."
- Backend auto-updates assigned employees' average ratings (handler does this).

If user has already reviewed (`order.review != null`), the review section renders read-only (Wave 1 already does this). Editing an existing review is also supported by backend (update-on-exists), but keep Wave 2 create-only. Edit is a trivial follow-up if needed.

### Files

- `features/orders/SubmitReviewSheet.kt` — new.
- `features/orders/OrderDetailScreen.kt` — replace "Coming soon" chip with active CTA that opens the sheet. Delete the placeholder chip composable.
- `features/orders/OrderDetailViewModel.kt` — add `submitReview(rating: Int, comment: String?)` method.

### i18n

- `order_review_sheet_title`, `order_review_rating_label`, `order_review_comment_placeholder`
- `order_review_submit`, `order_review_submitting`
- `order_review_success`

---

## Phase 4 — Download receipt

### UX

The Wave 1 "Coming soon" receipt button becomes active when `receiptNumber != null`. Tap → loading state on button → backend returns PDF bytes → save to `context.cacheDir/receipts/{orderId}.pdf` → launch `Intent.ACTION_VIEW` with a FileProvider URI, `application/pdf` mime type.

### Implementation notes

- `@Streaming` on the Retrofit method (Phase 1) — we buffer the full bytes into memory anyway (PDFs are small, ≤ 1MB typical), but the `ResponseBody` API is cleaner with streaming.
- FileProvider setup — check `AndroidManifest.xml`:
  - If `androidx.core.content.FileProvider` is already declared (common), reuse it with a `receipts` subfolder.
  - If not, add it + `res/xml/file_paths.xml` with `<cache-path name="receipts" path="receipts/"/>`.
- Mime type: `application/pdf`.
- Chooser intent to let user pick viewer/share target.
- Error case: if no PDF viewer installed, catch `ActivityNotFoundException` and show snackbar "No app available to open PDF".

### Files

- `features/orders/OrderDetailScreen.kt` — wire button click.
- `features/orders/OrderDetailViewModel.kt` — add `downloadReceipt(): Flow<File>` or similar, returns the saved file path on success.
- `core/orders/OrderRepository.kt` — `downloadReceipt` writes bytes to cache, returns `File`.
- Potentially `AndroidManifest.xml` + `res/xml/file_paths.xml` — only if FileProvider isn't already wired.

### i18n

- `order_receipt_loading`, `order_receipt_open_error`
- `order_receipt_no_viewer`

---

## Phase 5 — Photo gallery

### UX

OrderDetail gets a new section: "Photos" (shown only if backend response has `beforePhotoCount + afterPhotoCount > 0`, so a quick probe fetch on load is needed — or lazy-fetch on tap). Section shows: "Before ({count}) · After ({count})" summary + "View photos" button.

Tap → navigates to new `OrderPhotosScreen` (route `orders/{orderId}/photos`).

**OrderPhotosScreen**:
- TopAppBar with back arrow + title "Photos".
- Tab row: "Before ({count})" / "After ({count})".
- Each tab → lazy grid (3 columns, thumbnails) of `OrderPhotoDto` filtered by `photoType`.
- Tap thumb → fullscreen pager (`HorizontalPager`) with zoom-in capability. Dismiss = back.
- Empty tab state: "No {before|after} photos yet".

**SAS URL lifetime**: 1 hour. If the user keeps the screen open longer, pinch-zoom won't fail, but `AsyncImage` re-fetches will. Acceptable edge case — Wave 2 doesn't refresh; add a pull-to-refresh on the gallery if that matters later.

### Files

- `features/orders/photos/OrderPhotosScreen.kt` — new screen (tabs + grid).
- `features/orders/photos/OrderPhotosViewModel.kt` — fetches on init, exposes `UiState`.
- `features/orders/photos/OrderPhotoPagerScreen.kt` — fullscreen pager. Could be a separate destination or a fullscreen dialog state on the gallery screen (simpler).
- `navigation/Routes.kt` — add `OrderPhotos = "orders/{orderId}/photos"`.
- `navigation/CleansiaNavHost.kt` — wire the new route.
- `features/orders/OrderDetailScreen.kt` — add "Photos" section + nav callback.
- Potentially: a zoom-capable image composable. Coil already handles loading; for pinch-zoom, use `Modifier.pointerInput` + scale state, or add a small dep like `telephoto` (opinionated) / roll-your-own. Roll-your-own is ~40 lines and avoids a dep.

### i18n

- `order_photos_section_title`, `order_photos_view_button`
- `order_photos_tab_before`, `order_photos_tab_after`
- `order_photos_empty_before`, `order_photos_empty_after`

---

## Phase 6 — Disputes

### UX surfaces

Three screens total:

1. **Disputes list** (new tab? or accessed from settings/account?) — the web app has `/disputes` as a standalone route. Mobile should mirror: add a nav entry from the Profile/Account tab ("My disputes"), not a bottom-nav tab (disputes are infrequent).
2. **Create dispute sheet/screen** — bottom sheet reached from two places:
   - OrderDetail footer "Report issue" button (pre-fills orderId).
   - "New dispute" FAB on the disputes list (orderId selector required).
3. **Dispute detail** — messaging thread, list of messages, input box at bottom (if status allows new messages).

### Report issue CTA on OrderDetail

Replace current Wave 1 placeholder with an active "Report issue" button in the footer. Tap → opens `CreateDisputeSheet` with orderId pre-filled. On success → navigates to the newly-created dispute's detail.

### Create dispute form

Matches web:
- OrderId selector (disabled/pre-filled if coming from OrderDetail).
- Reason dropdown (fetch from backend enum if available; hardcode list if not — check `DisputeReason` enum in domain).
- Description textarea — min 10 chars, max 2000 chars.
- Primary: "Submit".

### Files (sizeable — this is the largest phase)

- `core/disputes/*` — already in Phase 1.
- `features/disputes/DisputesListScreen.kt` — new.
- `features/disputes/DisputesListViewModel.kt` — new.
- `features/disputes/CreateDisputeSheet.kt` — new.
- `features/disputes/DisputeDetailScreen.kt` — new.
- `features/disputes/DisputeDetailViewModel.kt` — new.
- `navigation/Routes.kt` — add `Disputes = "disputes"`, `DisputeDetail = "disputes/{id}"`, `CreateDispute = "disputes/new?orderId={orderId}"`.
- `navigation/CleansiaNavHost.kt` — wire all three routes.
- `features/profile/` — add "My disputes" row to the account tab that navigates to `Routes.Disputes`.
- `features/orders/OrderDetailScreen.kt` — "Report issue" button in footer.

### i18n

- `disputes_title`, `disputes_empty_title`, `disputes_empty_subtitle`, `disputes_empty_cta`
- `dispute_create_title`, `dispute_create_order_label`, `dispute_create_reason_label`, `dispute_create_description_label`, `dispute_create_description_min`, `dispute_create_submit`
- `dispute_detail_title`, `dispute_detail_status_{open|in_review|resolved|rejected}`
- `dispute_message_input_placeholder`, `dispute_message_send`
- `account_my_disputes` (profile tab entry)
- `order_action_report_issue`

Plus probably dispute reason enum keys if we hardcode them client-side.

---

## Execution plan

**Serial**:
- Phase 1 (data layer) — must land first, everything else depends on it.

**Parallel after Phase 1**:
- Phase 2 (cancel) — owns OrderDetailScreen footer area + new sheet.
- Phase 3 (review) — owns OrderDetailScreen review section + new sheet.
- Phase 4 (receipt) — owns OrderDetailScreen receipt button + cache/FileProvider wiring.
- Phase 5 (photos) — new screens, only touches OrderDetailScreen for the section add.
- Phase 6 (disputes) — new screens + profile tab row + OrderDetail footer button.

**Conflict points on OrderDetailScreen.kt**: phases 2, 3, 4, 6 all touch it (footer actions + sheet triggers + replacing Wave 1 placeholders). Options:
- (a) Serialize these four phases on OrderDetailScreen and parallelize only Phase 5.
- (b) Do an "OrderDetail footer refactor" pre-task that carves out the action-bar region cleanly, then parallelize.

Recommend (a): four agents sequentially on OrderDetail + one agent on photos (Phase 5) in parallel. Total ~5 agent runs.

## Out of scope (Wave 3+)

- Editing an existing review.
- Rebooking from a past order ("Book again" action).
- Disputes messaging — resolved/closed state handling, evidence uploads (web app has `DisputeEvidence` — mobile Wave 2 does basic messaging only).
- Push notifications for dispute replies / cancellation confirmations.
- Order photos — customer uploads (Wave 2 is view-only; backend exposes `UploadOrderPhoto` on Mobile API but it's scoped to cleaners).
