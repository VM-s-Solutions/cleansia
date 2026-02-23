package cz.cleansia.partner.features.orders.screens

import android.content.Intent
import android.provider.Settings
import androidx.core.app.NotificationManagerCompat
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material.icons.filled.Person
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
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
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.features.orders.components.ActionButtonSection
import cz.cleansia.partner.features.orders.components.AddNoteBottomSheet
import cz.cleansia.partner.features.orders.components.BeforeAfterPhotoSection
import cz.cleansia.partner.features.orders.components.CustomerContactCard
import cz.cleansia.partner.features.orders.components.CustomerInfoSection
import cz.cleansia.partner.features.orders.components.EmployeeNotesIssuesSection
import cz.cleansia.partner.features.orders.components.NotesInstructionsSection
import cz.cleansia.partner.features.orders.components.AuditInfoSection
import cz.cleansia.partner.features.orders.components.PaymentInfoSection
import cz.cleansia.partner.features.orders.components.QuickInfoCard
import cz.cleansia.partner.features.orders.components.ReportIssueBottomSheet
import cz.cleansia.partner.features.orders.components.ServicesSection
import cz.cleansia.partner.features.orders.components.TimerSection
import cz.cleansia.partner.features.orders.components.WorkflowStepperCard
import cz.cleansia.partner.features.orders.components.WorkflowStepperContent
import cz.cleansia.partner.features.orders.components.hasAnyNotes
import cz.cleansia.partner.features.orders.viewmodels.OrderDetailsUiState
import cz.cleansia.partner.features.orders.viewmodels.OrderDetailsViewModel
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.ui.components.CollapsibleSection
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.features.orders.components.OrderDetailsSkeleton

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

    // Check if notifications are enabled (Samsung/some OEMs disable by default on fresh install)
    val context = LocalContext.current
    var showNotificationDialog by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) {
            showNotificationDialog = true
        }
    }
    if (showNotificationDialog) {
        AlertDialog(
            onDismissRequest = { showNotificationDialog = false },
            title = { Text(stringResource(R.string.notification_enable_title)) },
            text = { Text(stringResource(R.string.notification_enable_message)) },
            confirmButton = {
                TextButton(onClick = {
                    showNotificationDialog = false
                    context.startActivity(
                        Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).apply {
                            putExtra(Settings.EXTRA_APP_PACKAGE, context.packageName)
                        }
                    )
                }) {
                    Text(stringResource(R.string.notification_enable_open_settings))
                }
            },
            dismissButton = {
                TextButton(onClick = { showNotificationDialog = false }) {
                    Text(stringResource(R.string.cancel))
                }
            }
        )
    }

    Scaffold { _ ->
        Box(
            modifier = Modifier
                .fillMaxSize()
        ) {
            when {
                uiState.isLoading -> {
                    OrderDetailsSkeleton(
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
                        onCompleteOrder = { viewModel.completeOrder() },
                        onUploadPhoto = { data, fileName, photoType -> viewModel.uploadPhoto(data, fileName, photoType) },
                        onUploadMultiplePhotos = { photosData, photoType -> viewModel.uploadMultiplePhotos(photosData, photoType) },
                        onDeletePhoto = { viewModel.deletePhoto(it) },
                        onShowPhotoValidation = { viewModel.showPhotoValidation() },
                        onAddNote = { viewModel.addNote(it) },
                        onReportIssue = { viewModel.reportIssue(it) }
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

            CleansiaSnackbarHost(hostState = snackbarHostState)
        }
    }
}

@Composable
private fun OrderDetailsContent(
    order: OrderDetail,
    uiState: OrderDetailsUiState,
    onTakeOrder: suspend () -> Boolean,
    onStartOrder: suspend () -> Boolean,
    onCompleteOrder: suspend () -> Boolean,
    onUploadPhoto: (ByteArray, String, PhotoType) -> Unit,
    onUploadMultiplePhotos: (List<Pair<ByteArray, String>>, PhotoType) -> Unit,
    onDeletePhoto: (String) -> Unit,
    onShowPhotoValidation: () -> Unit,
    onAddNote: (String) -> Unit,
    onReportIssue: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    var isServicesExpanded by remember { mutableStateOf(true) }
    var isCustomerInfoExpanded by remember { mutableStateOf(false) }
    var isPaymentExpanded by remember { mutableStateOf(false) }
    var isNotesExpanded by remember { mutableStateOf(false) }
    var isAuditExpanded by remember { mutableStateOf(false) }
    var isWorkflowExpanded by remember { mutableStateOf(false) }
    var showReportIssueDialog by remember { mutableStateOf(false) }
    var showAddNoteDialog by remember { mutableStateOf(false) }

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
                onReportIssue = { showReportIssueDialog = true },
                onAddNote = { showAddNoteDialog = true }
            )
        }

        // === SECTION 2: Quick Info Card (Address, Schedule, Property) ===
        QuickInfoCard(order = order)

        // === SECTION 3: Order Workflow Stepper ===
        if (order.status == OrderStatus.COMPLETED || order.status == OrderStatus.CANCELLED) {
            CollapsibleSection(
                title = stringResource(R.string.order_workflow),
                icon = Icons.Default.Check,
                isExpanded = isWorkflowExpanded,
                onToggle = { isWorkflowExpanded = !isWorkflowExpanded }
            ) {
                WorkflowStepperContent(
                    orderStatus = order.status,
                    isCurrentEmployeeAssigned = uiState.isCurrentEmployeeAssigned
                )
            }
        } else {
            WorkflowStepperCard(
                orderStatus = order.status,
                isCurrentEmployeeAssigned = uiState.isCurrentEmployeeAssigned
            )
        }

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

        // === SECTION 5: Progressive Disclosure Details ===

        // Services (default expanded - most useful info)
        CollapsibleSection(
            title = stringResource(R.string.services),
            icon = Icons.Default.Home,
            isExpanded = isServicesExpanded,
            onToggle = { isServicesExpanded = !isServicesExpanded }
        ) {
            ServicesSection(order = order)
        }

        // Customer Info
        CollapsibleSection(
            title = stringResource(R.string.customer),
            icon = Icons.Default.Person,
            isExpanded = isCustomerInfoExpanded,
            onToggle = { isCustomerInfoExpanded = !isCustomerInfoExpanded }
        ) {
            CustomerInfoSection(order = order)
        }

        // Payment Info
        CollapsibleSection(
            title = stringResource(R.string.payment_info),
            icon = Icons.Default.CreditCard,
            isExpanded = isPaymentExpanded,
            onToggle = { isPaymentExpanded = !isPaymentExpanded }
        ) {
            PaymentInfoSection(order = order)
        }

        // Notes & Instructions (only if any exist)
        if (hasAnyNotes(order)) {
            CollapsibleSection(
                title = stringResource(R.string.notes_instructions),
                icon = Icons.Default.Notes,
                isExpanded = isNotesExpanded,
                onToggle = { isNotesExpanded = !isNotesExpanded }
            ) {
                NotesInstructionsSection(order = order)
            }
        }

        // Audit Info (only if data exists)
        if (order.createdOn != null || order.updatedOn != null) {
            CollapsibleSection(
                title = stringResource(R.string.order_info),
                icon = Icons.Default.Info,
                isExpanded = isAuditExpanded,
                onToggle = { isAuditExpanded = !isAuditExpanded }
            ) {
                AuditInfoSection(order = order)
            }
        }

        // === SECTION 6: Employee Notes & Issues ===
        if (!order.orderNotes.isNullOrEmpty() || !order.orderIssues.isNullOrEmpty()) {
            EmployeeNotesIssuesSection(order = order)
        }

        // === SECTION 7: Before/After Photos (for IN_PROGRESS and COMPLETED orders) ===
        if (order.status == OrderStatus.IN_PROGRESS || order.status == OrderStatus.COMPLETED) {
            BeforeAfterPhotoSection(
                beforePhotos = uiState.beforePhotos,
                afterPhotos = uiState.afterPhotos,
                isUploading = uiState.isUploadingPhoto,
                canUpload = order.status == OrderStatus.IN_PROGRESS,
                showValidation = uiState.showPhotoValidation,
                onUploadPhoto = onUploadPhoto,
                onUploadMultiplePhotos = onUploadMultiplePhotos,
                onDeletePhoto = onDeletePhoto
            )
        }

        // === SECTION 8: Primary Action Button (at bottom for easy thumb reach) ===
        ActionButtonSection(
            orderStatus = order.status,
            isActionLoading = uiState.isActionLoading,
            hasRequiredPhotos = uiState.hasRequiredPhotos,
            isCurrentEmployeeAssigned = uiState.isCurrentEmployeeAssigned,
            hasOtherOrderInProgress = uiState.hasOtherOrderInProgress,
            onTakeOrder = onTakeOrder,
            onStartOrder = onStartOrder,
            onCompleteOrder = onCompleteOrder,
            onShowPhotoValidation = onShowPhotoValidation
        )

        Spacer(modifier = Modifier.height(16.dp))
    }

    // Report Issue Bottom Sheet
    if (showReportIssueDialog) {
        ReportIssueBottomSheet(
            onDismiss = { showReportIssueDialog = false },
            onSubmit = { issueText ->
                onReportIssue(issueText)
            }
        )
    }

    // Add Note Bottom Sheet
    if (showAddNoteDialog) {
        AddNoteBottomSheet(
            onDismiss = { showAddNoteDialog = false },
            onSave = { noteText ->
                onAddNote(noteText)
            }
        )
    }
}
