# Orders Integration — Wave 3 (customer Android + targeted backend)

Wave 1 = read path (list, detail, home recent, success). Wave 2 = order actions (cancel, review, receipt, photos) + customer disputes. Wave 3 = rebook, edit review, disputes evidence (render + upload), and a small backend foundations pass.

**Out of scope for Wave 3** (deferred to Wave 4 / polish):
- Push notifications (FCM + backend dispatch infrastructure — entire wave on its own)
- SAS URL refresh endpoint for photo gallery (acceptable edge case for Wave 3)

## Backend audit results (2026-04-25)

| Item | Backend status | Wave 3 work |
|---|---|---|
| Rebook | None — client-side replay (web does this via session storage) | Mobile only |
| Edit review | `SubmitOrderReview` is upsert by design; recalculates employee ratings on update | Mobile only |
| AuthorName empty | Mapper TODO at `DisputeMappers.cs` line ~59 | 30-min backend fix |
| Evidence rendering | `DisputeEvidenceDto` exists with `FileName, FilePath, UploadedBy, UploadedOn` — but no SAS-signed URL surfaced | Add SAS URL to mapper |
| Evidence upload | No endpoint exists; only employee photo upload (`UploadOrderPhoto`) | Net-new backend feature |

## Phases

### Phase B1 — Backend foundations (one agent run)

Three changes, all on the .NET backend. No EF migration needed (no schema changes — `DisputeEvidence` entity already exists).

**B1a — Fix `DisputeMessageDto.authorName`**
- File: `src/Cleansia.Core.AppServices/Mappers/DisputeMappers.cs` (TODO at the messages mapper)
- Update `MapToDetails` (or wherever messages are mapped) to project `firstName + " " + lastName` from the joined User. Either:
  - Eager-load the messages with `.Include(d => d.Messages).ThenInclude(m => m.Author)` in `GetDisputeDetails` handler, or
  - Project the join inside the IQueryable LINQ before materialization (preferred — avoids N+1).
- Decision: do the projection approach. Read the existing handler shape and match its style.
- For staff messages (`isStaffMessage = true`): if the Author is staff, prefer "Cleansia support" as the canonical label — but we'll let the mobile UI handle that mapping (Wave 2 already does). Backend just returns the actual user's name.

**B1b — SAS-signed URLs on `DisputeEvidenceDto`**
- File: `src/Cleansia.Core.AppServices/Features/Disputes/GetDisputeDetails.cs` + the evidence mapper
- After materializing the entities, walk evidence list and call `IBlobContainerClient.GenerateSasUri(blobName, TimeSpan.FromHours(1))` for each evidence's `FilePath` (which is the blob name).
- Add a new field to `DisputeEvidenceDto`: `BlobUrl: string?` (nullable in case generation fails). Keep existing fields untouched (`FileName`, `FilePath`, `UploadedBy`, `UploadedOn`).
- Match the pattern in `GetOrderPhotos.cs` for how SAS URIs are appended to a list of DTOs.
- Inject `IBlobContainerClientFactory` into the handler if not already present.
- Container: `Constants.BlobContainers.DisputeEvidence` — verify this constant exists; if not, add it under `Cleansia.Core.Domain/Constants.cs` or wherever blob container names live.

**B1c — `UploadDisputeEvidence` command + Customer API endpoint**
- New file: `src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs`
- New endpoint: `POST /api/Dispute/UploadEvidence` on `Cleansia.Web.Customer/Controllers/DisputeController.cs`
- Multipart form-data: `DisputeId` (string), `File` (IFormFile)
- Validation:
  - DisputeId required + exists
  - User must own the dispute (check `dispute.UserId == currentUserId`) OR be staff
  - File required, size ≤ 10MB
  - Content-Type whitelist: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`
- Handler:
  - Generate blob name: `disputes/{disputeId}/evidence/{Guid.NewGuid()}{extensionFromContentType}`
  - Upload via `IBlobContainerClient.UploadBlobAsync(blobName, stream, contentType)`
  - Create `DisputeEvidence` entity with `FileName = file.FileName, FilePath = blobName, UploadedBy = userId`
  - Persist via `IDisputeRepository`
  - Return `{ EvidenceId, BlobUrl }` (BlobUrl is the freshly-signed SAS, 1h TTL)
- Permission: new `Policy.CanUploadDisputeEvidence` — add to `Cleansia.Core.AppServices/Authentication/Policy.cs` + `PolicyBuilder.cs`. Grant to Customer + Admin.
- New `BusinessErrorMessage` keys: `dispute.evidence_too_large`, `dispute.evidence_unsupported_type`. Map to frontend i18n in Phase R1.
- Validator (FluentValidation, Cascade.Stop):
  - DisputeId NotEmpty + MustAsync(disputeRepository.ExistsAsync) + ownership check via async rule
  - File NotNull + Must(size <= 10MB) + Must(content type in whitelist)

**MANUAL_STEP for the user after B1**:
- Regenerate NSwag clients (`npm run generate-customer-client`) — DTO shape changed (`BlobUrl` added to evidence, new `UploadEvidence` endpoint, `authorName` populated).
- No EF migration needed.

---

### Phase M1 — Mobile data layer extensions

**Extend `core/disputes/DisputeDtos.kt`**:
- Add `blobUrl: String? = null` to `DisputeEvidenceDto`.
- Add `UploadDisputeEvidenceResponse(evidenceId: String?, blobUrl: String?)`.

**Extend `core/disputes/DisputeApi.kt`**:
- Add multipart upload endpoint:
```kotlin
@Multipart
@POST("api/Dispute/UploadEvidence")
suspend fun uploadEvidence(
    @Part("DisputeId") disputeId: RequestBody,
    @Part file: MultipartBody.Part,
): Response<UploadDisputeEvidenceResponse>
```

**Extend `core/disputes/DisputeRepository.kt`**:
- Add `suspend fun uploadEvidence(disputeId: String, fileBytes: ByteArray, fileName: String, mimeType: String): UploadDisputeEvidenceResponse?`
- Returns null on failure (snackbar surfaced — fetcher pattern, matching `cancel`/`submitReview`).
- After success, callers will trigger `getById` to refresh the dispute detail.

**No new files for rebook on the data side** — the rebook intake is presentation-layer state. The mobile booking module already has `BookingViewModel` accepting service ids / package ids / address fields; we just need to thread an "initial state" into it.

**No `OrderRepository` changes** — review edit reuses the existing `submitReview` method (backend is upsert).

---

### Phase R1 — Rebook from past order

**Mobile only.**

**Booking VM intake**:
- Add a new entry-point method on `BookingViewModel`: `prefillFromOrder(order: OrderDetailDto)`. Maps:
  - `selectedServiceIds = order.selectedServices?.mapNotNull { it.id }`
  - `selectedPackageIds = order.selectedPackages?.mapNotNull { it.id }`
  - `rooms = order.rooms`, `bathrooms = order.bathrooms`
  - Address: if the order's address matches a saved address (compare by full street+city+zip), use `savedAddressId`. Otherwise populate `street`, `city`, `zipCode` inline.
  - Date/time: do NOT pre-fill — user picks a new slot. Show today's date as default.
- Validate after pre-fill:
  - Cross-check service ids against current catalog (loaded by booking module). If any aren't in the active catalog, drop them silently and trigger a snackbar: "Some items from your previous booking are no longer available — please review your selection."
  - Same for packages.

**Nav wiring**:
- New route variant: `Routes.Booking` (if it exists; otherwise the booking sheet/screen entry point) takes an optional `rebookFromOrderId` query param. When present, the booking module:
  1. Fetches the order via `orderRepository.getById(rebookFromOrderId)`.
  2. Calls `bookingVm.prefillFromOrder(order)`.
  3. Opens the wizard at step 1.
- If the booking flow is a bottom sheet (Wave 1 used `BookingBottomSheet`): pass the orderId via a state holder or invoke a method on the sheet's VM directly.

**OrderDetailScreen footer**:
- Add a "Book again" button to `ActionsFooter` for orders with `status == Completed`.
- Style: filled primary button, text + icon (`Icons.Outlined.Refresh`).
- Tap → `onRebook(orderId)` → nav layer routes through to `BookingBottomSheet` (or whatever opens the wizard) with the rebook hint.
- Wire the existing `onRebook: () -> Unit` callback on `OrderDetailScreen` (currently `@Suppress("UNUSED_PARAMETER")`). Un-suppress.

**Wave 1 row chevron**: leave alone for now. The OrdersTab row still navigates to detail; the user can hit "Book again" from there.

**i18n** (EN + CS):
- `order_action_rebook` — "Book again" / "Objednat znovu"
- `order_rebook_unavailable_items` — "Some items from your previous booking are no longer available." / "Některé položky z vaší předchozí objednávky již nejsou dostupné."

---

### Phase R2 — Edit existing review

**Mobile only.**

**`SubmitReviewSheet` extension**:
- Add an optional param: `existingReview: OrderReviewDto? = null`. When non-null:
  - Pre-fill stars and comment text fields with the existing review's values.
  - Change title from "Rate your cleaning" → "Edit your review" (`order_review_edit_title`).
  - Change submit button label from "Submit review" → "Save changes" (`order_review_save`).
- The sheet passes `(rating, comment)` to its `onConfirm` callback regardless of mode.

**`OrderDetailViewModel`**:
- The existing `submitReview(rating, comment)` already works for upsert (backend handles it). No changes needed.

**`OrderDetailScreen.ReviewCard` rewrite**:
- When `order.review != null`:
  - Show existing read-only review (stars + comment, current Wave 1 layout).
  - Add an "Edit review" button below it (text button, primary tint).
  - Tap → set `showReviewSheet = true` and pass `existingReview = order.review` to the sheet.
- When `order.review == null` AND `status == Completed`:
  - Show the Wave 2 "Leave a review" button (current state).
  - Tap → set `showReviewSheet = true`, no `existingReview`.

**Snackbar message**: differentiate create vs edit:
- Create success: existing `order_review_success` ("Thanks — your review is in.")
- Edit success: new `order_review_updated` ("Review updated.")
- VM needs to know whether it was an edit. Simplest: add a `wasEdit: Boolean` flag to the SharedFlow emission, or store the previous-review-existed state at `submitReview` call time.

**i18n** (EN + CS):
- `order_review_edit_title` — "Edit your review" / "Upravit hodnocení"
- `order_review_edit_action` — "Edit review" / "Upravit hodnocení"
- `order_review_save` — "Save changes" / "Uložit změny"
- `order_review_updated` — "Review updated." / "Hodnocení aktualizováno."

---

### Phase D1 — Disputes evidence (render + upload)

**Mobile only** (backend foundations from B1 enable this).

**Render existing evidence on `DisputeDetailScreen`**:
- Replace Wave 2's `EvidenceNote` ("Open on web to view") with an inline gallery section.
- For each `DisputeEvidenceDto`:
  - If `fileName` extension is image (`.jpg`, `.jpeg`, `.png`, `.webp`) OR content type starts with `image/` (we don't have content type on the DTO — derive from extension): render a 72x72 thumbnail using `AsyncImage(model = blobUrl)`.
  - If `.pdf`: render a card with a PDF icon (`Icons.Outlined.PictureAsPdf`) + the filename.
  - On tap:
    - Image → open fullscreen pager (reuse `FullscreenPager` from Wave 2 photos, but with a single-item list).
    - PDF → reuse Wave 2 `openReceiptPdf` helper. Issue: that helper takes a `File`, not a URL. Need to either (a) download the blob to cache first then call the helper, or (b) make a new helper `openExternalPdf(context, url)` that hands the URL straight to `Intent.ACTION_VIEW`. Go with (a) for offline robustness — adds 1–2 seconds of wait but the PDF works without an internet round-trip when reopened.
  - Show evidence rows in a vertical list, with "By {uploadedBy} · {formatOrderDateTime(uploadedOn)}" caption below each row. The `uploadedBy` field is currently a user id — needs a lookup. Wave 3 acceptable: just show the formatted timestamp; drop the "by X" prefix unless the backend mapper enriches it. Document this gap.

**Upload new evidence**:
- Add an "Add evidence" button to `DisputeDetailScreen`, visible only when status allows new messages (reuse `disputeAllowsMessages(status)` from Wave 2).
- Tap → opens system file picker via `ActivityResultContracts.GetMultipleContents` with mime filter `["image/*", "application/pdf"]`.
- For each selected file:
  - Read the file via `context.contentResolver.openInputStream(uri).use { it.readBytes() }`.
  - Validate size ≤ 10MB client-side (snackbar if exceeded — don't even bother the backend).
  - Derive mime type via `context.contentResolver.getType(uri)` (fallback to `application/octet-stream`).
  - Derive filename via querying the OpenableColumns DocumentFile.
  - Call `disputeRepository.uploadEvidence(disputeId, bytes, fileName, mimeType)`.
  - Show a per-file progress indicator (small spinner overlay on a placeholder card while uploading).
- After all uploads finish (or each successful one — your call), trigger `viewModel.load()` to re-fetch the dispute detail with new evidence.

**`DisputeDetailViewModel` changes**:
- Add `uploadingEvidence: StateFlow<Boolean>` to gate the upload button.
- Add `uploadEvidence(file: EvidenceUpload)` method where `EvidenceUpload(bytes, fileName, mimeType)` is a simple data class.
- On success (response non-null), call `load()` to refresh.

**i18n** (EN + CS):
- `dispute_evidence_section_title` — "Evidence" / "Důkazy"
- `dispute_evidence_add_button` — "Add evidence" / "Přidat důkaz"
- `dispute_evidence_uploading` — "Uploading…" / "Nahrávám…"
- `dispute_evidence_too_large` — "File is too large (max 10 MB)." / "Soubor je příliš velký (max 10 MB)."
- `dispute_evidence_unsupported_type` — "Unsupported file type." / "Nepodporovaný typ souboru."
- `dispute_evidence_open_error` — "Couldn't open file." / "Soubor se nepodařilo otevřít."
- `dispute_evidence_pdf_label` — "PDF document" / "Dokument PDF"
- `dispute_evidence_caption` — "Added %1$s" / "Přidáno %1$s" (the formatted timestamp)

---

## Execution

1. **Phase B1** runs solo first. Backend changes only.
2. **MANUAL_STEP** — user regenerates NSwag clients. Confirm before proceeding.
3. **Phase M1** — small mobile data layer pass (DTOs + new API method + repo method).
4. **Phases R1 + R2 + D1** — can run in parallel after M1. Conflict points:
   - R1 + R2 both touch `OrderDetailScreen.kt` (footer for R1, ReviewCard for R2). Serialize R1 → R2 on that file.
   - D1 owns `DisputeDetailScreen.kt` exclusively.
   - So: launch D1 in parallel with R1; once R1 finishes, launch R2.

## Out of scope (Wave 4+)

- Push notifications (entire wave on its own — FCM + backend `INotificationService` + dispatch commands + token refresh + in-app settings)
- SAS URL refresh endpoint (photos gallery 1h TTL workaround)
- Evidence editing / deletion (Wave 3 is submit-only, mirroring how reviews started)
- "Book again" inline button on OrdersTab rows (Wave 3 lives only on detail screen)
- Customer photo upload to orders (separate from disputes — Wave 4 if requested)
