package cz.cleansia.customer.features.disputes

import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.Send
import androidx.compose.material.icons.outlined.AttachFile
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Image
import androidx.compose.material.icons.outlined.PictureAsPdf
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil3.compose.AsyncImage
import coil3.request.ImageRequest
import coil3.size.Size
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeDetailsDto
import cz.cleansia.customer.core.disputes.DisputeEvidenceDto
import cz.cleansia.customer.core.disputes.DisputeMessageDto
import cz.cleansia.customer.core.disputes.openEvidencePdfFromUrl
import cz.cleansia.customer.core.format.disputeAllowsMessages
import cz.cleansia.customer.core.format.disputeStatusColor
import cz.cleansia.customer.core.format.formatOrderDateTime
import cz.cleansia.customer.core.orders.ReceiptOpenResult
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.snackbar.SnackbarController
import cz.cleansia.customer.ui.snackbar.SnackbarControllerEntryPoint
import cz.cleansia.customer.ui.theme.Poppins
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Dispute detail screen — header + original description + message thread +
 * inline evidence section + reply input bar.
 *
 * The header surfaces status/reason/created-on so the user has context
 * without scrolling. The original description renders as the first "message
 * bubble" so the thread reads naturally top-to-bottom.
 *
 * Wave 3 Phase D1: evidence is now rendered inline (image thumbnails via Coil
 * 3, PDFs as a card with the PDF icon). Tapping an image opens a fullscreen
 * pinch-zoom preview; tapping a PDF downloads it to cache and hands off to
 * the system PDF viewer via FileProvider. An "Add evidence" button below the
 * list opens the system file picker (multi-select), with client-side
 * validation matching the backend's 10MB + image/PDF whitelist.
 *
 * Input bar gating: uses [disputeAllowsMessages] — hidden on Resolved /
 * Closed / Escalated. Null/unknown status shows the input (backend rejects
 * if disallowed). Same gate also hides the "Add evidence" button.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DisputeDetailScreen(
    onBack: () -> Unit = {},
    viewModel: DisputeDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val sending by viewModel.sending.collectAsStateWithLifecycle()
    val uploading by viewModel.uploadingEvidence.collectAsStateWithLifecycle()

    val loaded = state as? DisputeDetailViewModel.UiState.Loaded
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()

    // Snackbar controller for the local "open file" failures (no app to open
    // PDF / blob fetch failed). We don't go through the VM for these — they're
    // pure UI-side reactions to Intent / network outcomes from the helper.
    val snackbar: SnackbarController = remember {
        EntryPointAccessors
            .fromApplication(context, SnackbarControllerEntryPoint::class.java)
            .snackbarController()
    }
    val noViewerMessage = stringResource(R.string.dispute_evidence_no_viewer)
    val openErrorMessage = stringResource(R.string.dispute_evidence_open_error)

    // Fullscreen image preview overlay state. Holds the URL of the evidence
    // currently being viewed; null means no overlay. Same one-state-per-pager
    // pattern as the orders photo gallery, scaled down to a single image.
    var fullscreenImageUrl by remember { mutableStateOf<String?>(null) }

    // System file picker. `GetMultipleContents` accepts a single MIME filter
    // string; we use `*/*` so the user can select either images or PDFs in a
    // single picker session. The VM rejects unsupported types with a snackbar
    // — this is a pragmatic trade-off vs. a separate "Add image" / "Add PDF"
    // pair of buttons that would clutter the UI.
    val pickFiles = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetMultipleContents(),
        onResult = { uris ->
            if (uris.isEmpty()) return@rememberLauncherForActivityResult
            // Read each URI off the main thread; sequentially queue uploads
            // through the VM. Single-flight via `uploadingEvidence` flag means
            // each call awaits the previous one's reload — safe.
            coroutineScope.launch {
                for (uri in uris) {
                    // Read everything off the main thread — bytes, the binder
                    // IPC for getType, and the OpenableColumns query for the
                    // display name. Even though each individual call is fast,
                    // they add up across multi-file selections.
                    val (bytes, mime, fileName) = withContext(Dispatchers.IO) {
                        val bytes = runCatching {
                            context.contentResolver.openInputStream(uri)?.use { it.readBytes() }
                        }.getOrNull()
                        val mime = context.contentResolver.getType(uri) ?: "application/octet-stream"
                        val fileName = queryFileName(context, uri)
                            ?: "evidence-${System.currentTimeMillis()}"
                        Triple(bytes, mime, fileName)
                    }
                    if (bytes == null) continue
                    viewModel.uploadEvidence(bytes, fileName, mime)
                }
            }
        },
    )

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(
                            stringResource(R.string.dispute_detail_title),
                            style = MaterialTheme.typography.titleMedium.copy(
                                fontFamily = Poppins,
                                fontWeight = FontWeight.SemiBold,
                            ),
                        )
                        loaded?.dispute?.displayOrderNumber?.takeIf { it.isNotBlank() }?.let { num ->
                            Text(
                                "#$num",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.common_back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface,
                ),
            )
        },
        bottomBar = {
            // Only render input bar in Loaded state (we need the status to
            // decide gating). Loading/Error leave the slot empty.
            val dispute = loaded?.dispute
            if (dispute != null) {
                val allowsMessages = disputeAllowsMessages(dispute.status?.value)
                if (allowsMessages) {
                    MessageInputBar(
                        sending = sending,
                        onSend = viewModel::sendMessage,
                    )
                } else {
                    ClosedNote()
                }
            }
        },
    ) { padding ->
        Box(
            Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when (val s = state) {
                is DisputeDetailViewModel.UiState.Loading -> LoadingState()
                is DisputeDetailViewModel.UiState.Error -> ErrorState(
                    onRetry = viewModel::load,
                    onBack = onBack,
                )
                is DisputeDetailViewModel.UiState.Loaded -> LoadedContent(
                    dispute = s.dispute,
                    uploading = uploading,
                    onAddEvidenceClick = { pickFiles.launch("*/*") },
                    onImageEvidenceClick = { evidence ->
                        val url = evidence.blobUrl
                        if (url.isNullOrBlank()) {
                            snackbar.showError(openErrorMessage)
                        } else {
                            fullscreenImageUrl = url
                        }
                    },
                    onPdfEvidenceClick = { evidence ->
                        val id = evidence.id
                        val name = evidence.fileName
                        val url = evidence.blobUrl
                        if (id.isNullOrBlank() || name.isNullOrBlank() || url.isNullOrBlank()) {
                            snackbar.showError(openErrorMessage)
                        } else {
                            // Fire-and-forget on a coroutine — the helper
                            // internally forces IO dispatcher. The UI doesn't
                            // gate on a per-row spinner; the system file
                            // viewer launching is the visible feedback.
                            coroutineScope.launch {
                                val res = openEvidencePdfFromUrl(context, id, name, url)
                                when (res) {
                                    ReceiptOpenResult.Opened -> Unit
                                    ReceiptOpenResult.NoViewer -> snackbar.showError(noViewerMessage)
                                    is ReceiptOpenResult.Error -> snackbar.showError(openErrorMessage)
                                }
                            }
                        }
                    },
                    onUnknownEvidenceClick = { snackbar.showError(openErrorMessage) },
                )
            }
        }

        // Fullscreen image overlay sits on top of the Scaffold so it covers
        // the bottom bar / nav bar too. Wrapped in BackHandler-aware composable.
        fullscreenImageUrl?.let { url ->
            FullscreenImageOverlay(
                url = url,
                onClose = { fullscreenImageUrl = null },
            )
        }
    }
}

/* ── Loaded content ── */

@Composable
private fun LoadedContent(
    dispute: DisputeDetailsDto,
    uploading: Boolean,
    onAddEvidenceClick: () -> Unit,
    onImageEvidenceClick: (DisputeEvidenceDto) -> Unit,
    onPdfEvidenceClick: (DisputeEvidenceDto) -> Unit,
    onUnknownEvidenceClick: (DisputeEvidenceDto) -> Unit,
) {
    val allowsMessages = disputeAllowsMessages(dispute.status?.value)
    val evidence = dispute.evidence.orEmpty()

    // Single LazyColumn owning the whole thread so long message lists scroll
    // smoothly without the header shifting.
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(horizontal = 20.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        item { HeaderCard(dispute) }

        // Original description rendered as a user-authored message so the
        // thread reads naturally. `isStaffMessage = false` styling.
        dispute.description?.takeIf { it.isNotBlank() }?.let { desc ->
            item {
                MessageBubble(
                    message = DisputeMessageDto(
                        message = desc,
                        isStaffMessage = false,
                        createdOn = dispute.createdOn,
                    ),
                )
            }
        }

        dispute.messages?.forEach { msg ->
            item(key = msg.id ?: msg.hashCode()) {
                MessageBubble(message = msg)
            }
        }

        // Evidence section — inline list of rows + (when allowed) an Add button.
        // Skip the section header entirely on a closed dispute with no evidence
        // so we don't render a dangling "Evidence" title pointing at nothing.
        if (evidence.isNotEmpty() || allowsMessages) {
            item {
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(R.string.dispute_evidence_section_title),
                    style = MaterialTheme.typography.titleSmall.copy(
                        fontFamily = Poppins,
                        fontWeight = FontWeight.SemiBold,
                    ),
                    color = MaterialTheme.colorScheme.onBackground,
                )
            }
            evidence.forEach { ev ->
                item(key = ev.id ?: ev.hashCode()) {
                    EvidenceRow(
                        evidence = ev,
                        onClick = {
                            when {
                                isImageEvidence(ev) -> onImageEvidenceClick(ev)
                                isPdfEvidence(ev) -> onPdfEvidenceClick(ev)
                                else -> onUnknownEvidenceClick(ev)
                            }
                        },
                    )
                }
            }
            if (allowsMessages) {
                item {
                    AddEvidenceButton(
                        uploading = uploading,
                        onClick = onAddEvidenceClick,
                    )
                }
            }
        }

        item { Spacer(Modifier.height(24.dp)) }
    }
}

@Composable
private fun HeaderCard(dispute: DisputeDetailsDto) {
    val statusColor = disputeStatusColor(dispute.status?.value)
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(16.dp),
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                text = dispute.reason?.name ?: "—",
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            StatusPill(
                label = dispute.status?.name ?: "—",
                color = statusColor,
            )
        }
        Spacer(Modifier.height(4.dp))
        Text(
            text = formatOrderDateTime(dispute.createdOn),
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun StatusPill(label: String, color: Color) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.14f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

/* ── Message bubble ──
 *
 * Aligns right + primaryContainer bg for the customer, left + surfaceVariant
 * bg for staff. `authorName` on the wire is currently always empty string
 * (backend mapper has a TODO), so we synthesize the author label from the
 * `isStaffMessage` flag — which IS reliable per the wire contract.
 */
@Composable
private fun MessageBubble(message: DisputeMessageDto) {
    val isStaff = message.isStaffMessage
    val authorLabel = stringResource(
        if (isStaff) R.string.dispute_detail_author_support
        else R.string.dispute_detail_author_you,
    )
    val bubbleColor = if (isStaff) {
        MaterialTheme.colorScheme.surfaceVariant
    } else {
        MaterialTheme.colorScheme.primaryContainer
    }
    val textColor = if (isStaff) {
        MaterialTheme.colorScheme.onSurface
    } else {
        MaterialTheme.colorScheme.onPrimaryContainer
    }

    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (isStaff) Arrangement.Start else Arrangement.End,
    ) {
        Column(
            modifier = Modifier.fillMaxWidth(fraction = 0.85f),
            horizontalAlignment = if (isStaff) Alignment.Start else Alignment.End,
        ) {
            // Author label + timestamp above the bubble.
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Text(
                    authorLabel,
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    formatOrderDateTime(message.createdOn),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(4.dp))
            Box(
                modifier = Modifier
                    .clip(RoundedCornerShape(12.dp))
                    .background(bubbleColor)
                    .padding(horizontal = 14.dp, vertical = 10.dp),
            ) {
                Text(
                    text = message.message.orEmpty(),
                    style = MaterialTheme.typography.bodyMedium,
                    color = textColor,
                )
            }
        }
    }
}

/* ── Evidence row ── */

@Composable
private fun EvidenceRow(
    evidence: DisputeEvidenceDto,
    onClick: () -> Unit,
) {
    val isImage = isImageEvidence(evidence)
    val isPdf = isPdfEvidence(evidence)

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(12.dp),
            )
            .clickable(onClick = onClick)
            .padding(12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // Left: image thumbnail OR icon. The thumbnail uses the SAS-signed
        // blobUrl directly — Coil's OkHttp engine handles the download + cache.
        if (isImage && !evidence.blobUrl.isNullOrBlank()) {
            AsyncImage(
                model = evidence.blobUrl,
                contentDescription = stringResource(R.string.dispute_evidence_image_label),
                contentScale = ContentScale.Crop,
                modifier = Modifier
                    .size(56.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(MaterialTheme.colorScheme.surfaceVariant),
            )
        } else {
            Box(
                modifier = Modifier
                    .size(56.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(MaterialTheme.colorScheme.surfaceVariant),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    imageVector = when {
                        isPdf -> Icons.Outlined.PictureAsPdf
                        isImage -> Icons.Outlined.Image
                        else -> Icons.Outlined.AttachFile
                    },
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(28.dp),
                )
            }
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = evidence.fileName ?: when {
                    isPdf -> stringResource(R.string.dispute_evidence_pdf_label)
                    isImage -> stringResource(R.string.dispute_evidence_image_label)
                    else -> "—"
                },
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Medium),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = stringResource(
                    R.string.dispute_evidence_caption,
                    formatOrderDateTime(evidence.uploadedOn),
                ),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/* ── Add evidence button ── */

@Composable
private fun AddEvidenceButton(
    uploading: Boolean,
    onClick: () -> Unit,
) {
    FilledTonalButton(
        onClick = onClick,
        enabled = !uploading,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
    ) {
        if (uploading) {
            CircularProgressIndicator(
                strokeWidth = 2.dp,
                modifier = Modifier.size(18.dp),
                color = MaterialTheme.colorScheme.onSecondaryContainer,
            )
            Spacer(Modifier.width(10.dp))
            Text(stringResource(R.string.dispute_evidence_uploading))
        } else {
            Icon(
                imageVector = Icons.Outlined.AttachFile,
                contentDescription = null,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(stringResource(R.string.dispute_evidence_add_button))
        }
    }
}

/* ── Fullscreen image overlay ──
 *
 * Lightweight standalone preview rather than reusing FullscreenPager from the
 * orders module — that one is tightly typed to OrderPhotoDto and surfaces
 * "captured by {employee}" metadata, which doesn't exist for evidence. A
 * dedicated single-image viewer here keeps the code simple and avoids
 * synthesizing fake OrderPhotoDtos.
 *
 * No pinch-zoom for Wave 3; that's the conscious trade-off noted in the spec.
 * If users push back, lift ZoomableAsyncImage out of FullscreenPager into a
 * shared module and reuse here.
 */
@Composable
private fun FullscreenImageOverlay(
    url: String,
    onClose: () -> Unit,
) {
    // Cap decode resolution at the screen's longer side so a 12MP phone photo
    // doesn't decode at full resolution and pin the RenderThread on bitmap
    // upload. Without this hint, opening a high-res evidence image triggered a
    // full-app ANR (input dispatch >5s) — see ANR fa8ceb1 (2026-04-25).
    val context = LocalContext.current
    val config = LocalConfiguration.current
    val density = LocalDensity.current
    val maxPixels = remember(config, density) {
        val widthPx = with(density) { config.screenWidthDp.dp.toPx().toInt() }
        val heightPx = with(density) { config.screenHeightDp.dp.toPx().toInt() }
        maxOf(widthPx, heightPx).coerceAtLeast(1024)
    }
    val request = remember(url, maxPixels) {
        ImageRequest.Builder(context)
            .data(url)
            .size(Size(maxPixels, maxPixels))
            .build()
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.95f))
            // Tap anywhere on the scrim to dismiss — matches the orders pager UX.
            .pointerInput(Unit) { detectTapGestures(onTap = { onClose() }) },
    ) {
        AsyncImage(
            model = request,
            contentDescription = null,
            contentScale = ContentScale.Fit,
            modifier = Modifier.fillMaxSize(),
        )
        // Top-left close affordance (system-bar aware so the icon doesn't sit
        // under the status-bar inset on edge-to-edge builds).
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onClose) {
                Icon(
                    Icons.Outlined.Close,
                    contentDescription = stringResource(R.string.common_back),
                    tint = Color.White,
                )
            }
        }
        // Bottom inset spacer so any future overlay content (filename pill etc.)
        // doesn't collide with the gesture nav handle.
        Box(
            modifier = Modifier
                .navigationBarsPadding()
                .align(Alignment.BottomCenter),
        )
    }
    BackHandler(onBack = onClose)
}

/* ── Input bar / closed note ── */

@Composable
private fun MessageInputBar(
    sending: Boolean,
    onSend: (String) -> Unit,
) {
    var draft by remember { mutableStateOf("") }
    Surface(
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 0.dp,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .windowInsetsPadding(WindowInsets.navigationBars)
                .padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedTextField(
                value = draft,
                onValueChange = { next -> draft = if (next.length > 2000) next.substring(0, 2000) else next },
                placeholder = { Text(stringResource(R.string.dispute_detail_message_placeholder)) },
                maxLines = 4,
                enabled = !sending,
                shape = RoundedCornerShape(20.dp),
                modifier = Modifier.weight(1f),
                colors = TextFieldDefaults.colors(
                    focusedContainerColor = MaterialTheme.colorScheme.surface,
                    unfocusedContainerColor = MaterialTheme.colorScheme.surface,
                ),
            )
            Spacer(Modifier.width(8.dp))
            FilledIconButton(
                onClick = {
                    if (!sending && draft.isNotBlank()) {
                        onSend(draft)
                        draft = ""
                    }
                },
                enabled = !sending && draft.isNotBlank(),
                colors = IconButtonDefaults.filledIconButtonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ),
            ) {
                if (sending) {
                    CircularProgressIndicator(
                        strokeWidth = 2.dp,
                        color = MaterialTheme.colorScheme.onPrimary,
                        modifier = Modifier.size(18.dp),
                    )
                } else {
                    Icon(
                        Icons.AutoMirrored.Outlined.Send,
                        contentDescription = stringResource(R.string.dispute_detail_message_send),
                    )
                }
            }
        }
    }
}

@Composable
private fun ClosedNote() {
    Surface(
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 0.dp,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Text(
            text = stringResource(R.string.dispute_detail_closed_note),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier
                .fillMaxWidth()
                .windowInsetsPadding(WindowInsets.navigationBars)
                .padding(horizontal = 20.dp, vertical = 16.dp),
        )
    }
}

/* ── States ── */

@Composable
private fun LoadingState() {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
    }
}

@Composable
private fun ErrorState(
    onRetry: () -> Unit,
    onBack: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = stringResource(R.string.dispute_list_error_title),
            style = MaterialTheme.typography.titleMedium.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.SemiBold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(20.dp))
        CleansiaPrimaryButton(
            text = stringResource(R.string.dispute_list_error_retry),
            onClick = onRetry,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = stringResource(R.string.common_back),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onBack)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}

/* ── Helpers ──
 *
 * Backend doesn't include a content-type field on DisputeEvidenceDto, so we
 * derive image/PDF detection from the filename extension. This matches the
 * server-side whitelist (images: jpeg/jpg/png/webp/gif; documents: pdf) so
 * tap-handler routing aligns with what was actually uploaded.
 */

private fun isImageEvidence(evidence: DisputeEvidenceDto): Boolean {
    val name = evidence.fileName ?: return false
    val ext = name.substringAfterLast('.', "").lowercase()
    return ext in setOf("jpg", "jpeg", "png", "webp", "gif")
}

private fun isPdfEvidence(evidence: DisputeEvidenceDto): Boolean =
    evidence.fileName?.endsWith(".pdf", ignoreCase = true) == true

/**
 * Resolve the user-visible filename for a content URI. Returns null on any
 * provider error or when DISPLAY_NAME is missing — caller falls back to a
 * synthetic "evidence-{timestamp}" name.
 */
private fun queryFileName(context: android.content.Context, uri: Uri): String? {
    return runCatching {
        context.contentResolver.query(
            uri,
            arrayOf(android.provider.OpenableColumns.DISPLAY_NAME),
            null,
            null,
            null,
        )?.use { cursor ->
            if (cursor.moveToFirst()) cursor.getString(0) else null
        }
    }.getOrNull()
}
