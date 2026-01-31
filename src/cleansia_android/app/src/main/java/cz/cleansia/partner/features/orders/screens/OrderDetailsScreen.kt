package cz.cleansia.partner.features.orders.screens

import android.content.Intent
import android.net.Uri
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateContentSize
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Bathtub
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Directions
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Key
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.MeetingRoom
import androidx.compose.material.icons.filled.Message
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathFillType
import androidx.compose.ui.graphics.drawscope.clipPath
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.zIndex
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import kotlin.math.roundToInt
import kotlin.math.sqrt
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.domain.models.orders.ServiceDetail
import cz.cleansia.partner.features.orders.components.BeforeAfterPhotoSection
import cz.cleansia.partner.features.orders.viewmodels.OrderDetailsUiState
import cz.cleansia.partner.features.orders.viewmodels.OrderDetailsViewModel
import cz.cleansia.partner.ui.components.CleaningCountdownTimer
import cz.cleansia.partner.ui.components.DynamicCleaningBackground
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.components.OrderStatusBadge
import cz.cleansia.partner.ui.components.PaymentStatusBadge
import cz.cleansia.partner.ui.components.SwipeToConfirmButton
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.ui.theme.WorkflowColors
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

@Composable
fun OrderDetailsScreen(
    orderId: String,
    onNavigateBack: () -> Unit,
    viewModel: OrderDetailsViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Show error in snackbar
    LaunchedEffect(uiState.error, uiState.actionError) {
        (uiState.error ?: uiState.actionError)?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    // Show success message
    LaunchedEffect(uiState.actionSuccess) {
        uiState.actionSuccess?.let { message ->
            snackbarHostState.showSnackbar(message)
            viewModel.clearActionSuccess()
        }
    }

    // Show photo success/error
    LaunchedEffect(uiState.photoSuccess, uiState.photoError) {
        uiState.photoSuccess?.let { message ->
            snackbarHostState.showSnackbar(message)
            viewModel.clearPhotoSuccess()
        }
        uiState.photoError?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearPhotoError()
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { _ ->
        Box(
            modifier = Modifier
                .fillMaxSize()
        ) {
            when {
                uiState.isLoading -> {
                    LoadingIndicator(
                        modifier = Modifier.fillMaxSize()
                    )
                }
                uiState.error != null && uiState.order == null -> {
                    ErrorView(
                        message = uiState.error ?: "Unknown error",
                        onRetry = { viewModel.loadOrderDetails() },
                        modifier = Modifier.fillMaxSize()
                    )
                }
                uiState.order != null -> {
                    OrderDetailsContent(
                        order = uiState.order!!,
                        uiState = uiState,
                        onTakeOrder = { viewModel.takeOrder() },
                        onStartOrder = { viewModel.startOrder() },
                        onCompleteOrder = { viewModel.showCompleteOrderDialog() },
                        onUploadPhoto = { data, fileName, photoType -> viewModel.uploadPhoto(data, fileName, photoType) },
                        onDeletePhoto = { viewModel.deletePhoto(it) },
                        onShowPhotoValidation = { viewModel.showPhotoValidation() }
                    )
                }
            }

            GlassBackButton(
                onNavigateBack = onNavigateBack,
                title = uiState.order?.let {
                    stringResource(R.string.order_id, it.orderNumber)
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.background)
            )
        }
    }

    // Complete Order Dialog
    if (uiState.showCompleteDialog && uiState.order != null) {
        CompleteOrderDialog(
            estimatedTime = uiState.order!!.estimatedTime ?: 60,
            onDismiss = { viewModel.dismissCompleteOrderDialog() },
            onConfirm = { minutes, notes ->
                viewModel.completeOrder(minutes, notes)
            }
        )
    }
}

@Composable
private fun OrderDetailsContent(
    order: OrderDetail,
    uiState: OrderDetailsUiState,
    onTakeOrder: () -> Unit,
    onStartOrder: () -> Unit,
    onCompleteOrder: () -> Unit,
    onUploadPhoto: (ByteArray, String, PhotoType) -> Unit,
    onDeletePhoto: (String) -> Unit,
    onShowPhotoValidation: () -> Unit,
    modifier: Modifier = Modifier
) {
    var isDetailsExpanded by remember { mutableStateOf(false) }

    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .statusBarsPadding()
            .padding(start = 16.dp, end = 16.dp, top = 72.dp, bottom = 32.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // === SECTION 1: Timer with Quick Actions (only visible when IN_PROGRESS) ===
        AnimatedVisibility(
            visible = order.status == OrderStatus.IN_PROGRESS,
            enter = fadeIn() + expandVertically(),
            exit = fadeOut() + shrinkVertically()
        ) {
            TimerSection(
                estimatedMinutes = order.estimatedTime ?: 60,
                elapsedSeconds = uiState.elapsedSeconds,
                onReportIssue = { /* TODO: Implement report issue dialog */ },
                onAddNote = { /* TODO: Implement add note dialog */ }
            )
        }

        // === SECTION 2: Quick Info Card (Address, Schedule, Property) ===
        QuickInfoCard(order = order)

        // === SECTION 3: Order Workflow Stepper ===
        WorkflowStepperCard(orderStatus = order.status)

        // === SECTION 4: Customer Contact Card (visible for non-completed orders) ===
        AnimatedVisibility(
            visible = order.status != OrderStatus.COMPLETED && order.status != OrderStatus.CANCELLED,
            enter = fadeIn() + expandVertically(),
            exit = fadeOut() + shrinkVertically()
        ) {
            CustomerContactCard(
                customerName = order.customerName,
                customerPhone = order.customerPhone
            )
        }

        // === SECTION 5: Expandable Details ===
        ExpandableDetailsSection(
            order = order,
            isExpanded = isDetailsExpanded,
            onToggleExpanded = { isDetailsExpanded = !isDetailsExpanded }
        )

        // === SECTION 6: Before/After Photos (for IN_PROGRESS and COMPLETED orders) ===
        if (order.status == OrderStatus.IN_PROGRESS || order.status == OrderStatus.COMPLETED) {
            BeforeAfterPhotoSection(
                beforePhotos = uiState.beforePhotos,
                afterPhotos = uiState.afterPhotos,
                isUploading = uiState.isUploadingPhoto,
                canUpload = order.status == OrderStatus.IN_PROGRESS,
                showValidation = uiState.showPhotoValidation,
                onUploadPhoto = onUploadPhoto,
                onDeletePhoto = onDeletePhoto
            )
        }

        // === SECTION 7: Primary Action Button (at bottom for easy thumb reach) ===
        ActionButtonSection(
            orderStatus = order.status,
            isActionLoading = uiState.isActionLoading,
            hasRequiredPhotos = uiState.hasRequiredPhotos,
            onTakeOrder = onTakeOrder,
            onStartOrder = onStartOrder,
            onCompleteOrder = onCompleteOrder,
            onShowPhotoValidation = onShowPhotoValidation
        )

        Spacer(modifier = Modifier.height(16.dp))
    }
}

// ============================================================
// SECTION 1: Timer Section with Quick Actions
// ============================================================

@Composable
private fun TimerSection(
    estimatedMinutes: Int,
    elapsedSeconds: Long,
    onReportIssue: () -> Unit,
    onAddNote: () -> Unit
) {
    val isDarkTheme = LocalDarkTheme.current
    val density = LocalDensity.current
    val timerSize = 160.dp
    val cutoutGap = 7.dp
    val cutoutRadius = timerSize / 2 + cutoutGap
    val topPartHeight = 135.dp
    val buttonHeight = 64.dp
    val buttonSpacing = 10.dp // gap between top part bottom edge and buttons

    // Light background with dark icons
    val bgGradientStart = if (isDarkTheme) Color(0xFF1E293B) else Color(0xFFE0F2FE)
    val bgGradientEnd = if (isDarkTheme) Color(0xFF0F172A) else Color(0xFFF0F9FF)
    val iconColor = if (isDarkTheme) Color(0xFF7DD3FC) else Color(0xFF0284C7)
    val titleColor = if (isDarkTheme) Color(0xFFE0F2FE) else Color(0xFF0C4A6E)

    // Timer center sits at the bottom edge of the top part
    val timerCenterY = topPartHeight
    val timerTopY = timerCenterY - timerSize / 2

    // Buttons sit below the top part
    val buttonsTopY = topPartHeight + buttonSpacing

    // Total height: top part + spacing + buttons
    val totalHeight = buttonsTopY + buttonHeight

    // Pixel conversions for drawWithContent
    val cutoutRadiusPx = with(density) { cutoutRadius.toPx() }
    val timerCenterYPx = with(density) { timerCenterY.toPx() }

    // Timer center Y relative to the buttons Row top
    val timerCenterYRelativeToButtons = timerCenterY - buttonsTopY
    val timerCenterYRelativeToButtonsPx = with(density) { timerCenterYRelativeToButtons.toPx() }

    // Compute the circle intrusion into each button at the vertical center of the button,
    // so content can be centered within the visible width at that height.
    // Circle center is at y = timerCenterYRelativeToButtons (negative) relative to buttons top.
    // At y = buttonHeight/2 (vertical center of button):
    //   dy = buttonHeight/2 - timerCenterYRelativeToButtons
    //   halfChord = sqrt(r² - dy²)
    val buttonMidY = buttonHeight / 2
    val dy = buttonMidY - timerCenterYRelativeToButtons // distance from circle center to button mid
    val rVal = cutoutRadius.value
    val dyVal = dy.value
    val chordSquared = rVal * rVal - dyVal * dyVal
    // How far from screen center the circle extends at the button's vertical midpoint
    val circleHalfChordAtMid = if (chordSquared > 0) sqrt(chordSquared).dp else 0.dp

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(totalHeight)
    ) {
        // === TOP PART: Dynamic background with title + circular cutout ===
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(topPartHeight)
                .zIndex(1f)
                .drawWithContent {
                    val centerX = size.width / 2f
                    val cutoutPath = Path().apply {
                        fillType = PathFillType.EvenOdd
                        addRect(Rect(0f, 0f, size.width, size.height))
                        addOval(
                            Rect(
                                centerX - cutoutRadiusPx,
                                timerCenterYPx - cutoutRadiusPx,
                                centerX + cutoutRadiusPx,
                                timerCenterYPx + cutoutRadiusPx
                            )
                        )
                    }
                    clipPath(cutoutPath) {
                        this@drawWithContent.drawContent()
                    }
                }
        ) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .clip(RoundedCornerShape(12.dp))
                    .background(
                        Brush.linearGradient(
                            colors = listOf(bgGradientStart, bgGradientEnd)
                        )
                    )
            ) {
                DynamicCleaningBackground(
                    modifier = Modifier.fillMaxSize(),
                    iconColor = iconColor,
                    iconAlpha = if (isDarkTheme) 0.15f else 0.22f
                )

                Text(
                    text = stringResource(R.string.timer_cleaning_in_progress),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = titleColor,
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = 14.dp)
                )
            }
        }

        // === BOTTOM PART: Buttons with timer cutout ===
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(buttonHeight + 8.dp) // extra space for shadow overflow
                .offset(y = buttonsTopY)
                .zIndex(0f)
                .drawWithContent {
                    val centerX = size.width / 2f
                    val buttonsCutoutPath = Path().apply {
                        fillType = PathFillType.EvenOdd
                        addRect(Rect(0f, 0f, size.width, size.height))
                        addOval(
                            Rect(
                                centerX - cutoutRadiusPx,
                                timerCenterYRelativeToButtonsPx - cutoutRadiusPx,
                                centerX + cutoutRadiusPx,
                                timerCenterYRelativeToButtonsPx + cutoutRadiusPx
                            )
                        )
                    }
                    clipPath(buttonsCutoutPath) {
                        this@drawWithContent.drawContent()
                    }
                }
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(buttonHeight),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                TimerActionButton(
                    icon = Icons.Default.Warning,
                    label = stringResource(R.string.report_issue),
                    onClick = onReportIssue,
                    isWarning = true,
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                    side = ButtonSide.LEFT,
                    circleHalfChordAtMid = circleHalfChordAtMid,
                    gapBetweenButtons = 8.dp
                )

                TimerActionButton(
                    icon = Icons.Default.Notes,
                    label = stringResource(R.string.add_note),
                    onClick = onAddNote,
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                    side = ButtonSide.RIGHT,
                    circleHalfChordAtMid = circleHalfChordAtMid,
                    gapBetweenButtons = 8.dp
                )
            }
        }

        // === TIMER: Floating in the cutout ===
        Box(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .offset(y = timerTopY)
                .zIndex(2f)
                .size(timerSize)
                .shadow(
                    elevation = 4.dp,
                    shape = CircleShape
                ),
            contentAlignment = Alignment.Center
        ) {
            CleaningCountdownTimer(
                estimatedMinutes = estimatedMinutes,
                elapsedSeconds = elapsedSeconds,
                size = timerSize,
                strokeWidth = 10.dp
            )
        }
    }
}

@Composable
private fun TimerActionButton(
    icon: ImageVector,
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    isWarning: Boolean = false,
    side: ButtonSide = ButtonSide.LEFT,
    circleHalfChordAtMid: Dp = 0.dp,
    gapBetweenButtons: Dp = 8.dp
) {
    val backgroundColor = if (isWarning) {
        MaterialTheme.colorScheme.errorContainer
    } else {
        MaterialTheme.colorScheme.primaryContainer
    }

    val contentColor = if (isWarning) {
        MaterialTheme.colorScheme.onErrorContainer
    } else {
        MaterialTheme.colorScheme.onPrimaryContainer
    }

    val density = LocalDensity.current

    // How much the circle intrudes into this button at the content's vertical position.
    // circleHalfChordAtMid = distance from screen center to circle edge at button mid-height.
    // The button's inner edge is at gapBetweenButtons/2 from screen center.
    // Intrusion = circleHalfChordAtMid - gapBetweenButtons/2
    val intrusionIntoButton = (circleHalfChordAtMid - gapBetweenButtons / 2).coerceAtLeast(0.dp)

    // To center content in the visible width:
    // visibleWidth = buttonWidth - intrusion (the non-clipped portion)
    // center of visible = visibleWidth / 2 (from outer edge)
    // center of full button = buttonWidth / 2
    // offset = (center of visible) - (center of full button) = -intrusion / 2
    // LEFT button: shift left (toward outer edge); RIGHT button: shift right
    val offsetXPx = with(density) {
        val halfIntrusionPx = intrusionIntoButton.toPx() / 2f
        when (side) {
            ButtonSide.LEFT -> -halfIntrusionPx.roundToInt()
            ButtonSide.RIGHT -> halfIntrusionPx.roundToInt()
        }
    }

    Box(
        modifier = modifier
            .shadow(
                elevation = 4.dp,
                shape = RoundedCornerShape(12.dp)
            )
            .clip(RoundedCornerShape(12.dp))
            .background(backgroundColor)
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
            modifier = Modifier
                .offset { IntOffset(x = offsetXPx, y = 0) }
                .padding(vertical = 8.dp)
        ) {
            Icon(
                imageVector = icon,
                contentDescription = label,
                modifier = Modifier.size(22.dp),
                tint = contentColor
            )
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = FontWeight.Medium,
                color = contentColor,
                textAlign = TextAlign.Center,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

private enum class ButtonSide { LEFT, RIGHT }

// ============================================================
// Customer Contact Card - Available for all pre-completion statuses
// ============================================================

@Composable
private fun CustomerContactCard(
    customerName: String?,
    customerPhone: String?
) {
    val context = LocalContext.current

    // Only show if there's a phone number
    if (customerPhone.isNullOrBlank()) return

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            // Customer info
            Icon(
                imageVector = Icons.Default.Person,
                contentDescription = null,
                modifier = Modifier.size(20.dp),
                tint = MaterialTheme.colorScheme.primary
            )
            Text(
                text = customerName ?: stringResource(R.string.customer),
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f)
            )

            // Call button
            ContactActionButton(
                icon = Icons.Default.Phone,
                label = stringResource(R.string.call),
                onClick = {
                    val intent = Intent(Intent.ACTION_DIAL).apply {
                        data = Uri.parse("tel:$customerPhone")
                    }
                    context.startActivity(intent)
                }
            )

            // SMS button
            ContactActionButton(
                icon = Icons.Default.Message,
                label = stringResource(R.string.message),
                onClick = {
                    val intent = Intent(Intent.ACTION_SENDTO).apply {
                        data = Uri.parse("smsto:$customerPhone")
                    }
                    context.startActivity(intent)
                }
            )
        }
    }
}

@Composable
private fun ContactActionButton(
    icon: ImageVector,
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.primaryContainer)
            .clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = label,
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.primary
        )
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onPrimaryContainer
        )
    }
}

// ============================================================
// SECTION 3: Quick Info Card
// ============================================================

@Composable
private fun QuickInfoCard(order: OrderDetail) {
    val context = LocalContext.current

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = stringResource(R.string.quick_info),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    Text(
                        text = stringResource(R.string.payment_status_label),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    PaymentStatusBadge(status = order.paymentStatusEnum)
                }
            }

            // Address with navigation button
            if (order.fullAddress.isNotBlank()) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.surfaceVariant)
                        .clickable {
                            val encodedAddress = Uri.encode(order.fullAddress)
                            val gmmIntentUri = Uri.parse("geo:0,0?q=$encodedAddress")
                            val mapIntent = Intent(Intent.ACTION_VIEW, gmmIntentUri)
                            context.startActivity(mapIntent)
                        }
                        .padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.LocationOn,
                        contentDescription = null,
                        modifier = Modifier.size(24.dp),
                        tint = MaterialTheme.colorScheme.primary
                    )
                    Spacer(modifier = Modifier.width(12.dp))
                    Text(
                        text = order.fullAddress,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.weight(1f)
                    )
                    Icon(
                        imageVector = Icons.Default.Directions,
                        contentDescription = stringResource(R.string.open_in_maps),
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(24.dp)
                    )
                }
            }

            // Schedule info row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Scheduled date
                QuickInfoItem(
                    icon = Icons.Default.CalendarMonth,
                    label = stringResource(R.string.scheduled_date),
                    value = DateTimeUtils.formatDateTimeCompact(order.scheduledDate),
                    modifier = Modifier.weight(1f)
                )

                // Estimated time
                order.estimatedTime?.let { duration ->
                    QuickInfoItem(
                        icon = Icons.Default.AccessTime,
                        label = stringResource(R.string.estimated_time),
                        value = "$duration min",
                        modifier = Modifier.weight(1f)
                    )
                }
            }

            // Property info row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                order.rooms?.let { rooms ->
                    if (rooms > 0) {
                        QuickInfoItem(
                            icon = Icons.Default.MeetingRoom,
                            label = stringResource(R.string.rooms),
                            value = rooms.toString(),
                            modifier = Modifier.weight(1f)
                        )
                    }
                }

                order.bathrooms?.let { bathrooms ->
                    if (bathrooms > 0) {
                        QuickInfoItem(
                            icon = Icons.Default.Bathtub,
                            label = stringResource(R.string.bathrooms),
                            value = bathrooms.toString(),
                            modifier = Modifier.weight(1f)
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun QuickInfoItem(
    icon: ImageVector,
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
            .padding(8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(20.dp),
            tint = MaterialTheme.colorScheme.primary
        )
        Spacer(modifier = Modifier.width(8.dp))
        Column {
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(
                text = value,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium,
                color = MaterialTheme.colorScheme.onSurface
            )
        }
    }
}

// ============================================================
// SECTION 4: Order Workflow Stepper
// ============================================================

@Composable
private fun WorkflowStepperCard(orderStatus: OrderStatus) {
    val circleSize = 36.dp

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = stringResource(R.string.order_workflow),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                OrderStatusBadge(status = orderStatus)
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Circles row with connectors - circles centered in weighted columns
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Step 1 circle - centered in weighted column
                Box(
                    modifier = Modifier.weight(1f),
                    contentAlignment = Alignment.Center
                ) {
                    WorkflowStepCircle(
                        stepNumber = 1,
                        status = when (orderStatus) {
                            OrderStatus.PENDING -> StepStatus.CURRENT
                            else -> StepStatus.COMPLETED
                        },
                        size = circleSize
                    )
                }

                // Connector line - centered to circle height
                StepConnector(
                    isCompleted = orderStatus != OrderStatus.PENDING,
                    circleSize = circleSize
                )

                // Step 2 circle - centered in weighted column
                Box(
                    modifier = Modifier.weight(1f),
                    contentAlignment = Alignment.Center
                ) {
                    WorkflowStepCircle(
                        stepNumber = 2,
                        status = when (orderStatus) {
                            OrderStatus.PENDING -> StepStatus.PENDING
                            OrderStatus.CONFIRMED -> StepStatus.CURRENT
                            else -> StepStatus.COMPLETED
                        },
                        size = circleSize
                    )
                }

                // Connector line - centered to circle height
                StepConnector(
                    isCompleted = orderStatus == OrderStatus.IN_PROGRESS || orderStatus == OrderStatus.COMPLETED,
                    circleSize = circleSize
                )

                // Step 3 circle - centered in weighted column
                Box(
                    modifier = Modifier.weight(1f),
                    contentAlignment = Alignment.Center
                ) {
                    WorkflowStepCircle(
                        stepNumber = 3,
                        status = when (orderStatus) {
                            OrderStatus.IN_PROGRESS -> StepStatus.CURRENT
                            OrderStatus.COMPLETED -> StepStatus.COMPLETED
                            else -> StepStatus.PENDING
                        },
                        size = circleSize
                    )
                }
            }

            Spacer(modifier = Modifier.height(4.dp))

            // Labels row - spacers match StepConnector width (24.dp) for alignment
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.Top
            ) {
                WorkflowStepLabel(
                    title = stringResource(R.string.step_take_order),
                    status = when (orderStatus) {
                        OrderStatus.PENDING -> StepStatus.CURRENT
                        else -> StepStatus.COMPLETED
                    },
                    modifier = Modifier.weight(1f)
                )

                Spacer(modifier = Modifier.width(24.dp))

                WorkflowStepLabel(
                    title = stringResource(R.string.step_start_cleaning),
                    status = when (orderStatus) {
                        OrderStatus.PENDING -> StepStatus.PENDING
                        OrderStatus.CONFIRMED -> StepStatus.CURRENT
                        else -> StepStatus.COMPLETED
                    },
                    modifier = Modifier.weight(1f)
                )

                Spacer(modifier = Modifier.width(24.dp))

                WorkflowStepLabel(
                    title = stringResource(R.string.step_complete),
                    status = when (orderStatus) {
                        OrderStatus.IN_PROGRESS -> StepStatus.CURRENT
                        OrderStatus.COMPLETED -> StepStatus.COMPLETED
                        else -> StepStatus.PENDING
                    },
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

enum class StepStatus {
    PENDING, CURRENT, COMPLETED
}

@Composable
private fun WorkflowStepCircle(
    stepNumber: Int,
    status: StepStatus,
    size: Dp
) {
    val backgroundColor = when (status) {
        StepStatus.COMPLETED -> WorkflowColors.Completed
        StepStatus.CURRENT -> MaterialTheme.colorScheme.primary
        StepStatus.PENDING -> MaterialTheme.colorScheme.surfaceVariant
    }

    val contentColor = when (status) {
        StepStatus.COMPLETED -> Color.White
        StepStatus.CURRENT -> MaterialTheme.colorScheme.onPrimary
        StepStatus.PENDING -> MaterialTheme.colorScheme.onSurfaceVariant
    }

    Box(
        modifier = Modifier
            .size(size)
            .clip(CircleShape)
            .background(backgroundColor),
        contentAlignment = Alignment.Center
    ) {
        if (status == StepStatus.COMPLETED) {
            Icon(
                imageVector = Icons.Default.Check,
                contentDescription = null,
                tint = contentColor,
                modifier = Modifier.size(20.dp)
            )
        } else {
            Text(
                text = stepNumber.toString(),
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Bold,
                color = contentColor
            )
        }
    }
}

@Composable
private fun WorkflowStepLabel(
    title: String,
    status: StepStatus,
    modifier: Modifier = Modifier
) {
    Text(
        text = title,
        style = MaterialTheme.typography.labelSmall,
        color = if (status == StepStatus.CURRENT) MaterialTheme.colorScheme.primary
        else MaterialTheme.colorScheme.onSurfaceVariant,
        textAlign = TextAlign.Center,
        fontWeight = if (status == StepStatus.CURRENT) FontWeight.SemiBold else FontWeight.Normal,
        maxLines = 2,
        modifier = modifier
    )
}

@Composable
private fun StepConnector(
    isCompleted: Boolean,
    circleSize: Dp,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .width(24.dp)
            .height(circleSize),
        contentAlignment = Alignment.Center
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(2.dp)
                .background(
                    if (isCompleted) WorkflowColors.Completed
                    else MaterialTheme.colorScheme.outlineVariant
                )
        )
    }
}

// ============================================================
// SECTION 5: Action Button
// ============================================================

@Composable
private fun ActionButtonSection(
    orderStatus: OrderStatus,
    isActionLoading: Boolean,
    hasRequiredPhotos: Boolean,
    onTakeOrder: () -> Unit,
    onStartOrder: () -> Unit,
    onCompleteOrder: () -> Unit,
    onShowPhotoValidation: () -> Unit
) {
    when (orderStatus) {
        OrderStatus.PENDING -> {
            SwipeToConfirmButton(
                text = stringResource(R.string.swipe_to_take),
                onConfirm = onTakeOrder,
                enabled = !isActionLoading
            )
        }
        OrderStatus.CONFIRMED -> {
            SwipeToConfirmButton(
                text = stringResource(R.string.swipe_to_start),
                onConfirm = onStartOrder,
                enabled = !isActionLoading
            )
        }
        OrderStatus.IN_PROGRESS -> {
            SwipeToConfirmButton(
                text = stringResource(R.string.swipe_to_complete),
                onConfirm = onCompleteOrder,
                enabled = !isActionLoading,
                validateBeforeConfirm = {
                    if (!hasRequiredPhotos) {
                        onShowPhotoValidation()
                        false
                    } else {
                        true
                    }
                }
            )
        }
        OrderStatus.COMPLETED -> {
            // Show completed message
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(12.dp))
                    .background(CleansiaColors.successContainer)
                    .padding(16.dp),
                contentAlignment = Alignment.Center
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.Check,
                        contentDescription = null,
                        tint = CleansiaColors.onSuccessContainer,
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.order_completed_message),
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Medium,
                        color = CleansiaColors.onSuccessContainer
                    )
                }
            }
        }
        OrderStatus.CANCELLED -> {
            // Show cancelled message
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(12.dp))
                    .background(MaterialTheme.colorScheme.errorContainer)
                    .padding(16.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = stringResource(R.string.order_cancelled_message),
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Medium,
                    color = MaterialTheme.colorScheme.onErrorContainer
                )
            }
        }
    }
}

// ============================================================
// SECTION 6: Expandable Details
// ============================================================

@Composable
private fun ExpandableDetailsSection(
    order: OrderDetail,
    isExpanded: Boolean,
    onToggleExpanded: () -> Unit
) {
    val rotationAngle by animateFloatAsState(
        targetValue = if (isExpanded) 180f else 0f,
        label = "expandRotation"
    )

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .animateContentSize(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column {
            // Header (always visible)
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { onToggleExpanded() }
                    .padding(16.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = if (isExpanded) stringResource(R.string.hide_details)
                    else stringResource(R.string.view_details),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.primary
                )
                Icon(
                    imageVector = Icons.Default.KeyboardArrowDown,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.rotate(rotationAngle)
                )
            }

            // Expandable content
            AnimatedVisibility(
                visible = isExpanded,
                enter = fadeIn() + expandVertically(),
                exit = fadeOut() + shrinkVertically()
            ) {
                Column(
                    modifier = Modifier.padding(start = 16.dp, end = 16.dp, bottom = 16.dp),
                    verticalArrangement = Arrangement.spacedBy(16.dp)
                ) {
                    HorizontalDivider()

                    // Customer Info
                    CustomerInfoSection(order = order)

                    // Services
                    ServicesSection(order = order)

                    // Payment Info
                    PaymentInfoSection(order = order)

                    // Notes & Instructions
                    if (hasAnyNotes(order)) {
                        NotesInstructionsSection(order = order)
                    }

                    // Audit Info
                    if (order.createdOn != null || order.updatedOn != null) {
                        AuditInfoSection(order = order)
                    }
                }
            }
        }
    }
}

@Composable
private fun CustomerInfoSection(order: OrderDetail) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        SectionTitle(
            title = stringResource(R.string.customer),
            icon = Icons.Default.Person
        )

        order.customerName?.let { name ->
            DetailRowWithIcon(
                icon = Icons.Default.Person,
                label = stringResource(R.string.customer),
                value = name
            )
        }
        order.customerPhone?.let { phone ->
            if (phone.isNotBlank()) {
                DetailRowWithIcon(
                    icon = Icons.Default.Phone,
                    label = stringResource(R.string.phone),
                    value = phone
                )
            }
        }
        order.customerEmail?.let { email ->
            if (email.isNotBlank()) {
                DetailRowWithIcon(
                    icon = Icons.Default.Email,
                    label = stringResource(R.string.email),
                    value = email
                )
            }
        }
    }
}

@Composable
private fun ServicesSection(order: OrderDetail) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        SectionTitle(
            title = stringResource(R.string.services),
            icon = Icons.Default.Home
        )

        val servicesList = order.selectedServices ?: emptyList()

        if (servicesList.isEmpty()) {
            Text(
                text = stringResource(R.string.no_services),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        } else {
            servicesList.forEachIndexed { index, service ->
                ServiceItem(service = service, currencyCode = order.currencyCode)
                if (index < servicesList.size - 1) {
                    HorizontalDivider(
                        modifier = Modifier.padding(vertical = 4.dp),
                        color = MaterialTheme.colorScheme.outlineVariant
                    )
                }
            }

            HorizontalDivider(
                modifier = Modifier.padding(vertical = 8.dp)
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = stringResource(R.string.total),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = formatCurrency(order.totalAmount, order.currencyCode),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        }
    }
}

@Composable
private fun PaymentInfoSection(order: OrderDetail) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        SectionTitle(
            title = stringResource(R.string.payment_info),
            icon = Icons.Default.CreditCard
        )

        // Payment type
        order.paymentType?.name?.let { paymentType ->
            DetailRowWithIcon(
                icon = Icons.Default.CreditCard,
                label = stringResource(R.string.payment_type),
                value = paymentType
            )
        }

        // Payment status
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 4.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = stringResource(R.string.payment_status_label),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            PaymentStatusBadge(status = order.paymentStatusEnum)
        }
    }
}

@Composable
private fun NotesInstructionsSection(order: OrderDetail) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        SectionTitle(
            title = stringResource(R.string.notes_instructions),
            icon = Icons.Default.Notes
        )

        // Customer notes
        order.notes?.let { notes ->
            if (notes.isNotBlank()) {
                NoteBlock(
                    title = stringResource(R.string.customer_notes),
                    content = notes,
                    icon = Icons.Default.Notes
                )
            }
        }

        // Special instructions
        order.specialInstructions?.let { instructions ->
            if (instructions.isNotBlank()) {
                NoteBlock(
                    title = stringResource(R.string.special_instructions),
                    content = instructions,
                    icon = Icons.Default.Info
                )
            }
        }

        // Access instructions
        order.accessInstructions?.let { access ->
            if (access.isNotBlank()) {
                NoteBlock(
                    title = stringResource(R.string.access_instructions),
                    content = access,
                    icon = Icons.Default.Key
                )
            }
        }
    }
}

@Composable
private fun AuditInfoSection(order: OrderDetail) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        SectionTitle(
            title = stringResource(R.string.order_info),
            icon = Icons.Default.Info
        )

        order.createdOn?.let { created ->
            DetailRow(
                label = stringResource(R.string.created_on),
                value = created
            )
        }
        order.updatedOn?.let { updated ->
            DetailRow(
                label = stringResource(R.string.updated_on),
                value = updated
            )
        }
    }
}

// ============================================================
// Helper Components
// ============================================================

@Composable
private fun SectionTitle(
    title: String,
    icon: ImageVector
) {
    Row(
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(20.dp),
            tint = MaterialTheme.colorScheme.primary
        )
        Spacer(modifier = Modifier.width(8.dp))
        Text(
            text = title,
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.primary
        )
    }
}

@Composable
private fun NoteBlock(
    title: String,
    content: String,
    icon: ImageVector
) {
    Column {
        Row(
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                modifier = Modifier.size(16.dp),
                tint = MaterialTheme.colorScheme.primary
            )
            Spacer(modifier = Modifier.width(8.dp))
            Text(
                text = title,
                style = MaterialTheme.typography.labelMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.primary
            )
        }
        Spacer(modifier = Modifier.height(4.dp))
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(8.dp))
                .background(MaterialTheme.colorScheme.surfaceVariant)
                .padding(12.dp)
        ) {
            Text(
                text = content,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

private fun hasAnyNotes(order: OrderDetail): Boolean {
    return !order.notes.isNullOrBlank() ||
            !order.specialInstructions.isNullOrBlank() ||
            !order.accessInstructions.isNullOrBlank()
}

@Composable
private fun DetailRow(
    label: String,
    value: String
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

@Composable
private fun DetailRowWithIcon(
    icon: ImageVector,
    label: String,
    value: String
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(16.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.width(8.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

@Composable
private fun ServiceItem(service: ServiceDetail, currencyCode: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.Top
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = service.name ?: "",
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurface
            )
            if (!service.description.isNullOrBlank()) {
                Text(
                    text = service.description!!,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            service.estimatedTime?.let { time ->
                Text(
                    text = "$time min",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
        Text(
            text = formatCurrency(service.price ?: 0.0, currencyCode),
            style = MaterialTheme.typography.bodyLarge,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

private fun formatCurrency(amount: Double, currency: String = "CZK"): String {
    return try {
        val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
        format.currency = Currency.getInstance(currency)
        format.format(amount)
    } catch (e: Exception) {
        "$currency ${String.format("%.2f", amount)}"
    }
}

// ============================================================
// Complete Order Dialog
// ============================================================

@Composable
private fun CompleteOrderDialog(
    estimatedTime: Int,
    onDismiss: () -> Unit,
    onConfirm: (actualMinutes: Int, notes: String?) -> Unit
) {
    var actualMinutes by remember { mutableStateOf(estimatedTime.toString()) }
    var completionNotes by remember { mutableStateOf("") }

    val actualMinutesInt = actualMinutes.toIntOrNull() ?: 0
    val timeDifference = actualMinutesInt - estimatedTime
    val isDelayed = timeDifference > 0
    val delayText = when {
        timeDifference > 0 -> "+$timeDifference min (delayed)"
        timeDifference < 0 -> "$timeDifference min (early)"
        else -> "On time"
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Text(
                text = stringResource(R.string.complete_order),
                style = MaterialTheme.typography.headlineSmall
            )
        },
        text = {
            Column(
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Estimated time info
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text(
                        text = stringResource(R.string.estimated_time),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = "$estimatedTime min",
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Medium
                    )
                }

                // Actual completion time input
                OutlinedTextField(
                    value = actualMinutes,
                    onValueChange = { actualMinutes = it.filter { char -> char.isDigit() } },
                    label = { Text(stringResource(R.string.actual_completion_time)) },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )

                // Time difference indicator
                if (actualMinutes.isNotBlank()) {
                    Text(
                        text = delayText,
                        style = MaterialTheme.typography.bodySmall,
                        color = if (isDelayed) MaterialTheme.colorScheme.error
                        else CleansiaColors.success
                    )
                }

                // Completion notes
                OutlinedTextField(
                    value = completionNotes,
                    onValueChange = { completionNotes = it },
                    label = { Text(stringResource(R.string.completion_notes)) },
                    minLines = 3,
                    maxLines = 5,
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    val minutes = actualMinutes.toIntOrNull() ?: estimatedTime
                    onConfirm(minutes, completionNotes.ifBlank { null })
                },
                enabled = actualMinutes.isNotBlank() && (actualMinutes.toIntOrNull() ?: 0) > 0
            ) {
                Text(stringResource(R.string.confirm))
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text(stringResource(R.string.cancel))
            }
        }
    )
}
