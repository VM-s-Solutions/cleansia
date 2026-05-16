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
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Key
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.CurrencyUtils
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.ServiceDetail
import cz.cleansia.partner.ui.components.DetailRow
import cz.cleansia.partner.ui.components.DetailRowWithIcon
import cz.cleansia.partner.ui.components.PaymentStatusBadge
import cz.cleansia.partner.ui.components.SectionTitle

@Composable
internal fun CustomerInfoSection(order: OrderDetail) {
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
internal fun ServicesSection(order: OrderDetail) {
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
                    text = CurrencyUtils.formatCurrency(order.totalAmount, order.currencyCode),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        }
    }
}

@Composable
internal fun PaymentInfoSection(order: OrderDetail) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        SectionTitle(
            title = stringResource(R.string.payment_info),
            icon = Icons.Default.CreditCard
        )

        order.paymentType?.name?.let { paymentType ->
            DetailRowWithIcon(
                icon = Icons.Default.CreditCard,
                label = stringResource(R.string.payment_type),
                value = paymentType
            )
        }

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
internal fun NotesInstructionsSection(order: OrderDetail) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        SectionTitle(
            title = stringResource(R.string.notes_instructions),
            icon = Icons.Default.Notes
        )

        order.notes?.let { notes ->
            if (notes.isNotBlank()) {
                NoteBlock(
                    title = stringResource(R.string.customer_notes),
                    content = notes,
                    icon = Icons.Default.Notes
                )
            }
        }

        order.specialInstructions?.let { instructions ->
            if (instructions.isNotBlank()) {
                NoteBlock(
                    title = stringResource(R.string.special_instructions),
                    content = instructions,
                    icon = Icons.Default.Info
                )
            }
        }

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
internal fun AuditInfoSection(order: OrderDetail) {
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
                value = DateTimeUtils.formatDateTime(created)
            )
        }
        order.updatedOn?.let { updated ->
            DetailRow(
                label = stringResource(R.string.updated_on),
                value = DateTimeUtils.formatDateTime(updated)
            )
        }
    }
}

@Composable
internal fun NoteBlock(
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

internal fun hasAnyNotes(order: OrderDetail): Boolean {
    return !order.notes.isNullOrBlank() ||
            !order.specialInstructions.isNullOrBlank() ||
            !order.accessInstructions.isNullOrBlank()
}

@Composable
internal fun ServiceItem(service: ServiceDetail, currencyCode: String) {
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
            text = CurrencyUtils.formatCurrency(service.price ?: 0.0, currencyCode),
            style = MaterialTheme.typography.bodyLarge,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}
