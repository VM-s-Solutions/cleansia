package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.FrontHand
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.ui.components.SwipeToConfirmButton
import cz.cleansia.partner.ui.theme.CleansiaColors

@Composable
internal fun ActionButtonSection(
    orderStatus: OrderStatus,
    isActionLoading: Boolean,
    hasRequiredPhotos: Boolean,
    isCurrentEmployeeAssigned: Boolean = false,
    hasOtherOrderInProgress: Boolean = false,
    onTakeOrder: suspend () -> Boolean,
    onNotifyOnTheWay: suspend () -> Boolean,
    onStartOrder: suspend () -> Boolean,
    onCompleteOrder: suspend () -> Boolean,
    onShowPhotoValidation: () -> Unit
) {
    when (orderStatus) {
        OrderStatus.PENDING -> {
            if (isCurrentEmployeeAssigned) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(12.dp))
                        .background(MaterialTheme.colorScheme.primaryContainer)
                        .padding(16.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = stringResource(R.string.waiting_for_other_employees),
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Medium,
                        color = MaterialTheme.colorScheme.onPrimaryContainer,
                        textAlign = TextAlign.Center
                    )
                }
            } else {
                SwipeToConfirmButton(
                    text = stringResource(R.string.swipe_to_take),
                    onConfirm = onTakeOrder,
                    enabled = !isActionLoading,
                    icon = Icons.Default.FrontHand
                )
            }
        }
        OrderStatus.CONFIRMED -> {
            // Two-button stack:
            //   1) "Swipe to mark on the way" — sends `order.on_the_way`
            //      push to the customer; backend flips status to OnTheWay.
            //   2) "Swipe to start" — direct path. StartOrder validator
            //      accepts both Confirmed and OnTheWay so the cleaner
            //      can skip the heads-up if irrelevant.
            Column {
                if (hasOtherOrderInProgress) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(12.dp))
                            .background(MaterialTheme.colorScheme.errorContainer)
                            .padding(12.dp)
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                imageVector = Icons.Default.Info,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onErrorContainer,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                text = stringResource(R.string.api_error_order_employee_already_has_order_in_progress),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onErrorContainer
                            )
                        }
                    }
                    Spacer(modifier = Modifier.height(8.dp))
                }
                SwipeToConfirmButton(
                    text = stringResource(R.string.swipe_to_notify_on_the_way),
                    onConfirm = onNotifyOnTheWay,
                    enabled = !isActionLoading,
                    icon = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                )
                Spacer(modifier = Modifier.height(8.dp))
                SwipeToConfirmButton(
                    text = stringResource(R.string.swipe_to_start),
                    onConfirm = onStartOrder,
                    enabled = !isActionLoading && !hasOtherOrderInProgress,
                    icon = Icons.AutoMirrored.Filled.KeyboardArrowRight
                )
            }
        }
        OrderStatus.ON_THE_WAY -> {
            // Heads-up already sent — show only "Swipe to start" + a tag
            // confirming the customer was notified.
            Column {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(12.dp))
                        .background(MaterialTheme.colorScheme.primaryContainer)
                        .padding(12.dp),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = stringResource(R.string.customer_notified_on_the_way),
                        style = MaterialTheme.typography.bodySmall,
                        fontWeight = FontWeight.Medium,
                        color = MaterialTheme.colorScheme.onPrimaryContainer,
                        textAlign = TextAlign.Center,
                    )
                }
                Spacer(modifier = Modifier.height(8.dp))
                SwipeToConfirmButton(
                    text = stringResource(R.string.swipe_to_start),
                    onConfirm = onStartOrder,
                    enabled = !isActionLoading && !hasOtherOrderInProgress,
                    icon = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                )
            }
        }
        OrderStatus.IN_PROGRESS -> {
            SwipeToConfirmButton(
                text = stringResource(R.string.swipe_to_complete),
                onConfirm = onCompleteOrder,
                enabled = !isActionLoading,
                icon = Icons.Default.Check,
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
