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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.ui.components.OrderStatusBadge
import cz.cleansia.partner.ui.theme.WorkflowColors

@Composable
internal fun WorkflowStepperCard(
    orderStatus: OrderStatus,
    isCurrentEmployeeAssigned: Boolean = false
) {
    val circleSize = 36.dp

    val effectiveStep1Done = orderStatus != OrderStatus.PENDING || isCurrentEmployeeAssigned

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

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier.weight(1f),
                    contentAlignment = Alignment.Center
                ) {
                    WorkflowStepCircle(
                        stepNumber = 1,
                        status = when {
                            effectiveStep1Done -> StepStatus.COMPLETED
                            else -> StepStatus.CURRENT
                        },
                        size = circleSize
                    )
                }

                StepConnector(
                    isCompleted = effectiveStep1Done,
                    circleSize = circleSize
                )

                Box(
                    modifier = Modifier.weight(1f),
                    contentAlignment = Alignment.Center
                ) {
                    WorkflowStepCircle(
                        stepNumber = 2,
                        status = when {
                            orderStatus == OrderStatus.IN_PROGRESS || orderStatus == OrderStatus.COMPLETED -> StepStatus.COMPLETED
                            effectiveStep1Done -> StepStatus.CURRENT
                            else -> StepStatus.PENDING
                        },
                        size = circleSize
                    )
                }

                StepConnector(
                    isCompleted = orderStatus == OrderStatus.IN_PROGRESS || orderStatus == OrderStatus.COMPLETED,
                    circleSize = circleSize
                )

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

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.Top
            ) {
                WorkflowStepLabel(
                    title = stringResource(R.string.step_take_order),
                    status = when {
                        effectiveStep1Done -> StepStatus.COMPLETED
                        else -> StepStatus.CURRENT
                    },
                    modifier = Modifier.weight(1f)
                )

                Spacer(modifier = Modifier.width(24.dp))

                WorkflowStepLabel(
                    title = stringResource(R.string.step_start_cleaning),
                    status = when {
                        orderStatus == OrderStatus.IN_PROGRESS || orderStatus == OrderStatus.COMPLETED -> StepStatus.COMPLETED
                        effectiveStep1Done -> StepStatus.CURRENT
                        else -> StepStatus.PENDING
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

/**
 * Workflow stepper content without a Card wrapper, for use inside CollapsibleSection.
 */
@Composable
internal fun WorkflowStepperContent(
    orderStatus: OrderStatus,
    isCurrentEmployeeAssigned: Boolean = false
) {
    val circleSize = 36.dp
    val effectiveStep1Done = orderStatus != OrderStatus.PENDING || isCurrentEmployeeAssigned

    Column {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            OrderStatusBadge(status = orderStatus)
        }

        Spacer(modifier = Modifier.height(12.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(modifier = Modifier.weight(1f), contentAlignment = Alignment.Center) {
                WorkflowStepCircle(1, when { effectiveStep1Done -> StepStatus.COMPLETED; else -> StepStatus.CURRENT }, circleSize)
            }
            StepConnector(isCompleted = effectiveStep1Done, circleSize = circleSize)
            Box(modifier = Modifier.weight(1f), contentAlignment = Alignment.Center) {
                WorkflowStepCircle(2, when { orderStatus == OrderStatus.IN_PROGRESS || orderStatus == OrderStatus.COMPLETED -> StepStatus.COMPLETED; effectiveStep1Done -> StepStatus.CURRENT; else -> StepStatus.PENDING }, circleSize)
            }
            StepConnector(isCompleted = orderStatus == OrderStatus.IN_PROGRESS || orderStatus == OrderStatus.COMPLETED, circleSize = circleSize)
            Box(modifier = Modifier.weight(1f), contentAlignment = Alignment.Center) {
                WorkflowStepCircle(3, when (orderStatus) { OrderStatus.IN_PROGRESS -> StepStatus.CURRENT; OrderStatus.COMPLETED -> StepStatus.COMPLETED; else -> StepStatus.PENDING }, circleSize)
            }
        }

        Spacer(modifier = Modifier.height(4.dp))

        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
            WorkflowStepLabel(stringResource(R.string.step_take_order), when { effectiveStep1Done -> StepStatus.COMPLETED; else -> StepStatus.CURRENT }, Modifier.weight(1f))
            Spacer(modifier = Modifier.width(24.dp))
            WorkflowStepLabel(stringResource(R.string.step_start_cleaning), when { orderStatus == OrderStatus.IN_PROGRESS || orderStatus == OrderStatus.COMPLETED -> StepStatus.COMPLETED; effectiveStep1Done -> StepStatus.CURRENT; else -> StepStatus.PENDING }, Modifier.weight(1f))
            Spacer(modifier = Modifier.width(24.dp))
            WorkflowStepLabel(stringResource(R.string.step_complete), when (orderStatus) { OrderStatus.IN_PROGRESS -> StepStatus.CURRENT; OrderStatus.COMPLETED -> StepStatus.COMPLETED; else -> StepStatus.PENDING }, Modifier.weight(1f))
        }
    }
}

enum class StepStatus {
    PENDING, CURRENT, COMPLETED
}

@Composable
internal fun WorkflowStepCircle(
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
internal fun WorkflowStepLabel(
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
internal fun StepConnector(
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
