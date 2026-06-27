# Security findings — iOS partner Order PHOTO surface (T-0308: camera/library capture → base64 SavePhotos + DeletePhoto + GetPhotos on OrderDetail)

> Photo analogue of the T-0307 order-action gate (`security/ios-orders.md`). Same posture: backend
> traced + VERIFIED on this Mac; the iOS capture/upload/delete client is greenfield (no capture code
> on disk yet), so the binding rules below are what the developer builds to and the reviewer enforces.

## 2026-06-27 — T-0308 Partner photo upload/delete gate (Gate-SEC, security reviewer) — PASS-the-design (binding client rules) + ONE EXIF REQUIREMENT (P3) + backend VERIFIED

**security_touching: YES.** **Verdict: PASS-the-iOS-design (binding client rules P1–P5) — the
backend SavePhotos/DeletePhoto/GetPhotos ownership scoping is VERIFIED safe on the reachable backend
(no CHANGES on backend). The one DESIGN REQUIREMENT on the iOS/architect side is P3: EXIF/GPS
stripping must be an EXPLICIT, asserted guarantee — not an incidental side-effect of the JPEG
re-encode.** Scope = the **photo upload/delete/read ownership + image-PII** security gate of T-0308
(the architect rules capture/compression/image-loading in parallel — I stay out of those, except to
make the EXIF guarantee a *binding* property of whatever encode path they choose).

**State of the world:** `phase/ios-phase4` carries T-0307 order code + the **generated** photo DTOs
(`CleansiaPartnerApi/Models/{SaveOrderPhotosCommand,SaveOrderPhotosPhotoToSave,SaveOrderPhotosResponse,
DeleteOrderPhotoResponse,UploadOrderPhotoResponse,GetOrderPhotosOrderPhotoDto,GetOrderPhotosResponse,
PhotoType}.swift`), but **no iOS capture / image-picker / UIImage / camera code on disk** (tree grep
clean). So T-0308 is greenfield on the client — these are build-to rules, not findings against shipped
iOS code. T-0307's §7.8 / `ios-orders.md` rulings (O1–O4) carry over unchanged; this note adds the
photo-specific gates.

### DECISION 1 — IS T-0308 security_touching? **YES.**
It is a **state-changing write surface on a shared resource** (order photos): `orderSavePhotos`
(creates blob + DB rows under the caller's assignment), `orderDeletePhoto` (deletes a blob + DB row),
and `orderGetPhotos` (returns time-limited SAS URLs to customer-property imagery + the capturing
cleaner's identity). The after-photo flips the server-recomputed `OrderItem.hasAfterPhotos`, which
gates Complete — so the write has a **lifecycle/side-effect consequence**. It moves customer-property
imagery (potential PII-in-pixels) and, if EXIF survives, precise GPS of the cleaner/customer into
stored blobs. Gate 3 applies (endpoint + resource-by-id + side-effecting command + DTO + PII + file
upload).

### What was read (trace base — backend reachable on this Mac, same discipline as T-0307 / Devices D8)
- Controller routes: `Web.Mobile.Partner/Controllers/OrderController.cs:110-159` (UploadPhoto,
  SavePhotos, GetPhotos, DeletePhoto).
- Write handlers: `Core.AppServices/Features/Orders/SaveOrderPhotos.cs`, `UploadOrderPhoto.cs`.
- Delete handler: `Core.AppServices/Features/Orders/DeleteOrderPhoto.cs`.
- Read handler: `Core.AppServices/Features/Orders/GetOrderPhotos.cs`.
- Authz seam: `Core.AppServices/Authentication/OrderAccessService.cs`
  (`GetCallerEmployeeIdAsync`, `CanBrowseOrderAsync`, `IsCustomerCaller`).
- Entity + repo: `Core.Domain/Orders/OrderPhoto.cs` (`: Auditable, ITenantEntity`);
  `Infra.Database/Repositories/OrderPhotoRepository.cs`; `Infra.Database/BaseRepository.cs:15-39`
  (`ExistsAsync`/`GetByIdAsync` go through `GetDbSet()`/`GetQueryable()` → tenant filter applies).
- SAS shape: `Infra.Azure.Storage.Blobs/BlobContainerClient.cs:86-113`
  (`BlobSasPermissions.Read`, `ExpiresOn = now + 1h`, signed per blob name).
- Generated iOS DTOs (no hand-written auth/header/401 — ADR-0019 spine):
  `cleansia_ios/CleansiaPartnerApi/Models/SaveOrderPhotos*.swift`, `DeleteOrderPhotoResponse.swift`,
  `GetOrderPhotos*.swift`.

### DECISION 2 — UPLOAD/DELETE/READ OWNERSHIP (the O1/O2 analogue, the load-bearing check) — VERIFIED SAFE

- **S1 (actor = JWT, never a client field) — PASS (VERIFIED).** `SaveOrderPhotos.Command(OrderId,
  Photos)` and `DeleteOrderPhoto.Command(PhotoId)` carry **no client `employeeId`/`userId`**. The
  acting employee is resolved server-side via `orderAccessService.GetCallerEmployeeIdAsync`
  (`OrderAccessService.ResolveCallerEmployeeIdAsync`: employee-id claim → fallback
  `GetByUserEmailAsync(jwt email)`). The `capturedByEmployeeId` stamp on each `OrderPhoto.Create` is
  the **server** `employeeId` (`SaveOrderPhotos.cs:139`, `UploadOrderPhoto.cs:116`), never a client
  value. The `CapturedAt` is `DateTime.UtcNow` server-side (`OrderPhoto.cs:82`). PASS.

- **S2 (authorization) — PASS (VERIFIED).** Every route has a `[Permission]`:
  `CanUploadOrderPhoto` (UploadPhoto + SavePhotos), `CanViewOrderPhotos` (GetPhotos),
  `CanDeleteOrderPhoto` (DeletePhoto) — `OrderController.cs:111,124,137,149`. No `[AllowAnonymous]`,
  no missing attribute.

- **S3 (SavePhotos scopes by the caller's assignment) — PASS (VERIFIED).** The handler loads the
  order with `.Include(o => o.AssignedEmployees)` and rejects unless
  `order.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId)` (`SaveOrderPhotos.cs:91-104`;
  identical guard in `UploadOrderPhoto.cs:80-93`). A partner uploading to a **foreign** order →
  `EmployeeNotAssignedToOrder`. (Same existence-revealing-not-404 caveat as the T-0307 action paths —
  acceptable: write denied, no customer data returned.) PASS.

- **S3 (DeletePhoto resolves ownership SERVER-SIDE from photoId → order → caller — the subtle one) —
  PASS (VERIFIED).** This is the rule the prompt flagged (the contract carries only `photoId`). The
  handler does NOT trust any client-supplied order/owner: it loads the photo by id
  (`photoRepository.GetByIdAsync(command.PhotoId)`, `DeleteOrderPhoto.cs:39`), derives the caller
  from the JWT (`:45`), loads `photo.OrderId`'s order with its assigned employees (`:51-54`), and
  rejects unless `order != null && order.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId)`
  (`:56-59`). **The server resolves `photoId → order → caller-assignment`** — the client cannot name
  the owner. A partner deleting **another order's** photo → `EmployeeNotAssignedToOrder`. PASS.
  *Note:* DeletePhoto is author-agnostic among assigned cleaners — ANY currently-assigned employee can
  delete ANY photo on that order (no `capturedByEmployeeId == caller` author check, unlike notes/issues
  in T-0307). On single-cleaner jobs (today's reality) this is moot. On shared jobs it means cleaner B
  can delete cleaner A's before/after photo. This is a **product/authorship choice, not a leak** (the
  photo stays within the one order the deleter is legitimately on; nothing crosses orders/tenants).
  Logged as a LOW-severity authorship hardening for the shared-jobs era — NOT a blocker, NOT a T-0308
  iOS concern. (Cross-ref the T-0307 note/issue author-scoping precedent.)

- **S3/S4 (GetPhotos read-scoping) — PASS (VERIFIED, and BETTER than GetPaged).** Unlike the
  T-0307 `GetPaged` over-read gap, `GetOrderPhotos.Handler` gates the WHOLE read on
  `CanBrowseOrderAsync(order)` (`GetOrderPhotos.cs:57-62`): the caller must be admin, the order's
  customer, an assigned employee, OR an employee browsing an order that `HasAvailableSpots`
  (`OrderAccessService.cs:68-86`). A partner who guesses a foreign `orderId` they are not assigned to
  and that has no open spots → `OrderNotFound` (existence hidden — correct). **No `photoId`-keyed read
  endpoint exists** (you fetch photos only by an order you can browse), so there is no
  guess-a-photoId disclosure path. PASS.

### DECISION 3 — EXIF / PII-IN-IMAGE (S4-adjacent) — **DESIGN REQUIREMENT P3 (EXIF strip must be EXPLICIT + ASSERTED)**

**RULING: GPS/EXIF stripping MUST be an explicit, guaranteed, test-asserted property of the upload
path — NOT merely an incidental side-effect of the architect's JPEG re-encode.** Rationale:

- Camera-captured images carry EXIF: **GPS lat/long** (the cleaner's — i.e. the customer's home —
  precise position), device model, OS version, capture timestamp, sometimes serial numbers. The
  backend stores the uploaded bytes verbatim to blob (`SaveOrderPhotos.cs:123-127` streams the decoded
  base64 straight to `UploadAsync` — **no server-side EXIF scrub**). Whatever EXIF the client sends is
  what gets stored and later handed out via SAS URL. **The client is the only place EXIF can be
  stripped** on this path.
- `UIImage.jpegData(compressionQuality:)` (the architect's likely re-encode) **does drop EXIF/GPS** as
  a side effect — but "side effect of an unrelated decision" is exactly the fragile coupling the law
  warns against (S4: "even if every field is safe today…"). If the architect later switches the encode
  to a path that preserves metadata (e.g. `PHAsset` resource copy, `ImageIO` passthrough, or a
  "lossless" library-asset upload that skips re-encode), GPS silently starts leaking into stored blobs
  with **zero** signal. So: make EXIF-free an **invariant of the upload boundary**, independent of how
  the compression is implemented.

**Binding (P3):** the upload path MUST produce an **EXIF-free / GPS-free** JPEG before base64
encoding, and there MUST be a **test/assertion** that the encoded output carries no GPS (and ideally
no EXIF IFD at all). Recommended: route ALL captured/library images through the re-encode (so
`UIImage.jpegData` is the single chokepoint that guarantees no metadata), and add an assertion that
parses the produced JPEG (`CGImageSourceCopyPropertiesAtIndex` /
`kCGImagePropertyGPSDictionary == nil`) → no GPS dictionary. If the architect adopts a no-re-encode
library-asset fast-path for any reason, P3 still binds: that path must run an explicit metadata-strip
step. This is the photo analogue of "never return an entity — map to a DTO": never upload the raw
camera asset — always upload the metadata-stripped re-encode.

### DECISION 4 — PERMISSION-DENIAL UX + UPLOAD BOUNDS

- **Permission-denial UX (P4) — BINDING-lite (client design).** Denied camera/library permission MUST
  degrade cleanly: a clear message and (recommended) a Settings deep-link
  (`UIApplication.openSettingsURLString`) — **NOT a silent dead control**. The capture affordance must
  not appear to do nothing. (Parity with the Devices/RegistrationLock "fail-closed, never silent"
  posture.) Not a leak; a UX-correctness gate to avoid a confused-deputy dead-end. Also: `Info.plist`
  MUST declare `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` (Apple-required;
  app-review gate) — flag to the architect/owner as a manifest manual step.

- **Upload bounds (S5) — PASS (VERIFIED) with a client-side cooperation rule.**
  - **Server size cap:** `SaveOrderPhotos.Validator` enforces `MaxFileSizeBytes = 10 MB` per photo via
    `GetBase64DataSize(base64) <= MaxFileSizeBytes` (`SaveOrderPhotos.cs:37,60-64,68-73`);
    `UploadOrderPhoto.Validator` enforces the same 10 MB on the byte array (`UploadOrderPhoto.cs:33,
    57-61`). So a 50 MB single photo is rejected. **Note (LOW, latent):** `SavePhotos` caps each photo
    at 10 MB but does **not** cap the *count* of photos in one command — N×10 MB in a single JSON body
    is theoretically possible. Bounded in practice by the route rate-limit (below) + Kestrel's request
    body limit, and the architect's downscale keeps real photos well under 10 MB. Logged as a LOW
    backend hardening (cap photos-per-request, e.g. ≤ 10); NOT a T-0308 blocker.
  - **Rate limit (S5):** SavePhotos + UploadPhoto + DeletePhoto all carry
    `[EnableRateLimiting("auth")]` (`OrderController.cs:112,125,150`) — the partitioned shared window
    (per-JWT-sub for authenticated callers; ADR-0003 / ADR-RATELIMIT). A partner cannot hammer the
    upload route to DoS or mass-store. PASS. GetPhotos (read, no side-effect) is unlimited — acceptable.
  - **Client cooperation (P5):** the architect's downscale/compress MUST run **before** base64 so the
    client doesn't ship oversized bodies that the server then rejects (UX) — and so the 10 MB server
    cap is a backstop, not the primary bound. (Architect owns the downscale target; security only
    requires the result be EXIF-free per P3 and under the server cap.)

- **SAS URL scoping / non-enumerability (S4) — PASS (VERIFIED).** `GetOrderPhotos` returns SAS URLs
  (`GenerateSasUrl`, `GetOrderPhotos.cs:71,104-127`), not raw container URLs. Each SAS is
  `BlobSasPermissions.Read` only, `ExpiresOn = UtcNow + 1h`, and **signed per specific blob name**
  (`BlobContainerClient.cs:86-113`) — it is a per-blob, time-bounded, read-only capability. A partner
  CANNOT enumerate another order's photos by guessing: (a) they never get a foreign order's photo list
  (GetPhotos is `CanBrowseOrderAsync`-gated, DECISION 2), and (b) the blob name embeds
  `{year}/{orderId}/{orderId}_{type}_{utcTimestamp}_{8-hex-guid}{ext}` AND requires a valid SAS
  signature — the random GUID + the cryptographic SAS make direct blob access non-forgeable. PASS.
  *Latent (LOW):* the stored `BlobUrl` and the returned SAS are not tenant-path-segmented beyond
  `orderId`, but `OrderPhoto : ITenantEntity` + the order-level browse gate mean no cross-tenant read
  is reachable; the SAS expiry (1h) also bounds any leaked-URL window. Adequate.

### DECISION (the rest of the S-walk) — S6 / S7 / S8 / S9 / S10

- **S6 (logging) — PASS.** The photo handlers log nothing (no `_logger` at all); no customer
  email/phone/name/address/coords above Debug. The iOS client adds none (ADR-0019 spine).
- **S7 (idempotency) — PASS, with a LOW double-upload note.** Photo upload is not a money/email side
  effect (no Stripe/loyalty/referral/invoice) so S7's "must be idempotent" bar is N/A in the strict
  sense. BUT: a stale-client retry of `SavePhotos` (network hiccup, double-tap) creates a **second
  blob + second DB row** for the same logical photo (each `OrderPhoto` gets a fresh
  `Guid`/`uniqueFileName`, no dedupe). Consequence is benign (a duplicate before/after photo, and
  `hasAfterPhotos` is just a `count > 0` so Complete-gating is unaffected) — NOT a doubled financial
  side-effect, NOT a blocker. The **client must re-entry-guard the upload button while a SavePhotos
  call is in flight** (P-O4 parity) to avoid the obvious double-tap duplicate. Logged as LOW.
- **S8 (tenant isolation) — PASS (VERIFIED).** `OrderPhoto : Auditable, ITenantEntity` —
  global EF filter auto-scopes. The repo reads (`GetPhotosByOrderIdAsync`,
  `GetPhotoCountByOrderIdAndTypeAsync`) and `BaseRepository.ExistsAsync`/`GetByIdAsync` all go through
  `GetDbSet()`/`GetQueryable()` (filter applies — no `FromSqlRaw`, no leaked `IQueryable`, no one-sided
  join). The order load uses `.Include(AssignedEmployees)` on the tenant-filtered `Order` set. No
  cross-tenant photo read/write reachable. PASS.
- **S9 (migration / DTO contract) — N/A for T-0308.** The `OrderPhoto` table + the photo DTOs already
  exist and are already NSwag-generated for iOS (Models present). T-0308 adds **client capture code
  only** — no backend schema/DTO change, so no `ef-migration` / `nswag-regen` manual step is forced by
  the iOS work. (If the architect's metadata adds `Width`/`Height` population via a new field, that
  would be a contract change — but the DTO already carries nullable `Width`/`Height`, so even that is
  additive/nullable-safe.)
- **S10 (soft-delete) — PASS.** Photo delete is a hard delete (`photoRepository.Remove(photo)`,
  `DeleteOrderPhoto.cs:71`) + best-effort blob delete (`:61-69`, swallowed catch — acceptable: orphan
  blob is a cleanup concern, not a security one; the DB row is the source of truth and it's gone, and
  the SAS to the orphan still expires in 1h). No `IsActive` soft-delete semantics on photos to leak.

### Binding rules (the iOS developer builds to these; the reviewer enforces them)

**RULE P1 — JWT-derived actor (BINDING, mirrors S1 / O1).** The iOS client MUST NOT add any
`employeeId`/`userId`/`capturedById`/`capturedAt` to the SavePhotos or DeletePhoto body. SavePhotos
carries `{orderId, photos:[{photoType, file{fileName,base64Content,contentType}, notes}]}` ONLY;
DeletePhoto carries `{photoId}` ONLY. The capturing employee + capture time are server-stamped.
*Reviewer grep:* no actor/identity field added to either generated command on the client construction
sites.

**RULE P2 — no-id-echo (BINDING, the Devices D8 / O2 analogue).** The iOS client MUST upload ONLY to
its **constructed/loaded `orderId`** (the id the OrderDetail route was opened with — itself a loaded
list-row id; no synthesized/guessed/cross-screen order id) and MUST delete ONLY a `photoId` it
received from THIS order's own `orderGetPhotos` response. No free-form id input, no UUID-literal, no
photoId carried from another screen. *Reviewer grep:* every `orderId` fed to SavePhotos traces to the
detail VM's own `orderId`; every `photoId` fed to DeletePhoto traces to a row of the loaded
`getPhotos` response.

**RULE P3 — explicit EXIF/GPS strip (BINDING — DECISION 3).** The upload path produces an EXIF-free /
GPS-free JPEG, guaranteed by an explicit re-encode (not an incidental one), with a test asserting the
encoded output has no GPS dictionary. If any no-re-encode fast-path is introduced, it must run an
explicit metadata-strip. *Reviewer check:* the encode chokepoint is single + asserted EXIF-free.

**RULE P4 — clean permission-denial (BINDING-lite).** Denied camera/library permission shows a clear
message + (recommended) a Settings deep-link — never a silent dead control. `Info.plist` declares
`NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription`. *Reviewer check:* the denied path has a
visible affordance; the manifest strings exist.

**RULE P5 — client downscale before encode + re-entry guard (BINDING-lite, S5/S7 cooperation).** The
client downscales/compresses before base64 (architect owns the target) so bodies stay under the 10 MB
server cap; the capture/upload control is re-entry-guarded while a SavePhotos call is in flight (no
double-tap duplicate, mirrors O4). The server-side 10 MB cap + `auth` rate-limit are the backstops, not
the primary bound. *Reviewer check:* upload button disabled while submitting; no raw full-res asset
shipped.

> Client rail-gating (canUploadBefore/After by order status) is **UI-only, NOT a security boundary** —
> the server is authority (assignment + permission). Mirror the T-0307 `completeBlocked` precedent: the
> client gates the affordance for UX, the backend handler is the enforcement. A bypassing client that
> POSTs SavePhotos still hits the assignment check (`SaveOrderPhotos.cs:101-104`).

### Required test (Gate 6)

- **TC-IOS-PHOTOS-OWNERSHIP (red-first, client VM):** (a) SavePhotos is built ONLY from the detail
  VM's own loaded `orderId` (P1+P2) — assert no employeeId/actor field on the command and no
  synthesized/foreign orderId path; (b) DeletePhoto is built ONLY from a `photoId` present in this
  order's own `getPhotos` response (P2) — assert the delete call's id traces to a loaded photo row,
  never a literal/free-form id; (c) on a backend rejection (`EmployeeNotAssignedToOrder` /
  `OrderNotFound`) the client shows a clean message + refreshes the photo list — no crash, no
  optimistic double-state; (d) the upload control is re-entry-guarded — a second SavePhotos fired while
  one is in flight is dropped (assert `commands.count == 1`, the FakePartnerOrderClient suspension-gate
  pattern from T-0307).
- **TC-IOS-PHOTOS-EXIF-STRIP (red-first, encode assertion — gates P3):** feed the encode path an image
  carrying a GPS EXIF dictionary; assert the produced JPEG (what gets base64'd into
  `BlobFileDto.base64Content`) has **no `kCGImagePropertyGPSDictionary`** (and ideally no EXIF IFD)
  when parsed with `CGImageSourceCopyPropertiesAtIndex`. This is the load-bearing P3 guarantee — it
  must fail if someone swaps in a metadata-preserving encode.

### Open follow-ups for the backend owner (NONE block T-0308 iOS; all LOW/latent)
1. **DeletePhoto authorship (LOW, latent).** On shared jobs (`MaxEmployees > 1`), any assigned cleaner
   can delete another's photo (no `capturedByEmployeeId == caller` check). Add an author check (mirror
   the T-0307 note/issue author-scoping) before shared jobs ship. Dormant on single-cleaner jobs.
2. **SavePhotos photos-per-request cap (LOW).** Cap the count of photos in one SavePhotos command
   (e.g. ≤ 10) so the per-photo 10 MB cap can't be multiplied into a large body. Backstopped by the
   `auth` rate-limit + Kestrel body limit today.
3. **(Optional hardening)** server-side EXIF scrub on upload as defense-in-depth behind the client P3
   strip (so a non-iOS/legacy client can't store GPS-bearing blobs). Low priority — the partner clients
   are the only writers and both are under our control.

## 2026-06-27 — T-0308 Slice B BUILD-TIME VERIFICATION (Gate-SEC, security reviewer) — PASS (verified-against-code)

**Verdict: PASS.** The uncommitted Slice B code on `phase/ios-phase4` satisfies every binding rule
(P1–P5), the required test (TC-IOS-PHOTOS-OWNERSHIP) exists and passes, and the AR-PRIV plist keys are
present. This is the build-time confirmation of the 2026-06-27 PASS-the-design ruling above —
verified against the actual code, not the design. No code edits made (audit-only).

**Files verified (uncommitted; `git status` = modified + untracked):**
`Sources/Features/Orders/OrderPhotosViewModel.swift` (new), `Sources/Features/Orders/PhotosSection.swift`
(new), `Sources/Data/PartnerOrderClient.swift` (modified), `Tests/OrderPhotosViewModelTests.swift` (new),
`Tests/FakePartnerOrderClient.swift` (modified), `Info.plist` + `project.yml` (modified), the 5
`Resources/{en,cs,sk,uk,ru}.lproj/InfoPlist.strings` (new), plus the Slice A chokepoint
`CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift` and
`CleansiaCore/Sources/CleansiaCore/Components/CameraOrLibraryPicker.swift`.

- **P1 (no client actor on write surface) — PASS.** `PartnerOrderClient.savePhoto(orderId, photoType,
  base64Content, fileName, contentType)` and `deletePhoto(photoId)` carry NO employeeId/userId/actor
  (`PartnerOrderClient.swift:40-47`). `LivePartnerOrderClient.savePhoto` builds `SaveOrderPhotosCommand{
  orderId, photos:[{photoType, BlobFileDto{fileName,base64Content,contentType}, notes:nil}]}` ONLY
  (`:202-220`); `deletePhoto` sends only `photoId` (`:222-226`). VM comment + construction confirm
  (`OrderPhotosViewModel.swift:67-74`). Grep of the photo path for employeeId/capturedBy/userId/actor =
  no actor on any photo surface (the only `employeeId` hits are the unrelated "mine"-pane query filter).
  The actor is server-stamped (`capturedByEmployeeId`, backend VERIFIED in the design ruling above).
- **P2 (no id-echo) — PASS.** VM uploads/reads ONLY its constructed `private let orderId`
  (`OrderPhotosViewModel.swift:26,41,68`), which `OrderDetailView` hands it from the route-opened id
  (`OrderDetailView.swift:13,35` — same id as the detail VM, checklist VM, notes VM; no synthesis).
  `delete(photoId:)` takes an id the UI drew from a loaded `getPhotos` row (`PhotosSection.swift:133`
  `onDelete(photo.id)` over the loaded `ForEach(photos)`). No free-form/UUID-literal/cross-screen id.
  Pinned by TC-IOS-PHOTOS-OWNERSHIP: `testSavePhotoCarriesOnlyConstructedOrderIdNoEmployeeId`
  (asserts orderId == "order-xyz", one command) + `testDeleteActsOnlyOnAPhotoIdFromItsOwnGetPhotos`
  (asserts the deleted id traces to the loaded row "owned-photo").
- **P3 (EXIF strip, integration reaffirmed) — PASS.** Upload routes the captured `UIImage` through
  `ImageCompressor.encode` (`OrderPhotosViewModel.swift:57-59`) and base64s ONLY that output
  (`:71`). Grep of the ENTIRE iOS tree for `jpegData`/`pngData` = only inside `ImageCompressor.swift`
  itself (`:35,75`, its own ImageIO helper) — no raw `UIImage.jpegData`/original-bytes fallback
  anywhere on the photo path. The encoder strips metadata by construction (fresh CGContext redraw +
  ImageIO destination with an empty properties dict, only `kCGImageDestinationLossyCompressionQuality`
  — `ImageCompressor.swift:47-86`). Slice A's TC-IOS-PHOTOS-EXIF-STRIP asserts no
  `kCGImagePropertyGPSDictionary` on the output (`ImageCompressorTests.swift:59-77`).
- **P4 (permission-denial UX) — PASS.** Camera denial is reachable and degrades cleanly:
  `requestCamera()` switches on `AVCaptureDevice.authorizationStatus`; `.denied/.restricted` and a
  declined `requestAccess` both set `showPermissionAlert` (`PhotosSection.swift:140-153`), whose alert
  offers a Settings deep-link via `UIApplication.openSettingsURLString` (`:114-119,155-158`) — not a
  silent dead control. L10n keys present (`L10n+Orders.swift:264,268,272`).
- **P5 (off-main compress + re-entry guard) — PASS.** Compress runs off the main actor via
  `Task.detached(priority:.userInitiated)` (`OrderPhotosViewModel.swift:57-59`). Upload guarded by
  `guard !mutation.isUploading` (`:53`); delete guarded by `guard mutation.deletingId == nil` (`:80`).
  Both verified by the FakePartnerOrderClient suspension-gate tests
  (`testReentryGuardDropsSecondUploadWhileSubmitting`, `testReentryGuardDropsSecondDeleteWhileSubmitting`)
  asserting the in-flight command count stays at 1.

**Required test — TC-IOS-PHOTOS-OWNERSHIP: EXISTS AND PASSES.** Ran
`xcodebuild test -only-testing:CleansiaPartnerTests/OrderPhotosViewModelTests` on iOS 26.2 sim
(2026-06-27): **Executed 11 tests, 0 failures** — TEST SUCCEEDED. Includes both ownership tests + both
re-entry-guard tests + upload/delete success/failure + categorization. (TC-IOS-PHOTOS-EXIF-STRIP lives
in CleansiaCoreTests, Slice A.)

**AR-PRIV plist keys — PRESENT.** `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` in
`Info.plist:23-26` AND in `project.yml:45-46` (generated build carries them), plus localized in all 5
languages under `Resources/{en,cs,sk,uk,ru}.lproj/InfoPlist.strings`.

**ADR-0019 spine — PASS.** Photo calls ride the generated `PartnerOrderAPI.orderSavePhotos /
orderDeletePhoto / orderGetPhotos` via the shared `apiResult(mapError:)` seam — no new
token/header/401 path. Server remains authoritative on ownership + the 10 MB/photo cap + `auth`
rate-limit (backend §7.10 verification unchanged by this client slice). The two LOW/latent owner
follow-ups (DeletePhoto authorship on shared jobs; photos-per-request cap) remain open and unchanged.

**Gaps: NONE that block.** Slice B ships clean against the binding gate.
