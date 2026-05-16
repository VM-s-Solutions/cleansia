package cz.cleansia.partner.features.profile.components.documents

import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.DocumentType

@Composable
internal fun getDocumentTypeName(type: DocumentType): String {
    return when (type) {
        DocumentType.ID_CARD -> stringResource(R.string.doc_id_card)
        DocumentType.PASSPORT -> stringResource(R.string.doc_passport)
        DocumentType.DRIVING_LICENSE -> stringResource(R.string.doc_driving_license)
        DocumentType.WORK_PERMIT -> stringResource(R.string.doc_work_permit)
        DocumentType.CONTRACT -> stringResource(R.string.doc_contract)
        DocumentType.CERTIFICATE -> stringResource(R.string.doc_certificate)
        DocumentType.BANK_STATEMENT -> stringResource(R.string.doc_bank_statement)
        DocumentType.TAX_DOCUMENT -> stringResource(R.string.doc_tax_document)
        DocumentType.INSURANCE_DOCUMENT -> stringResource(R.string.doc_insurance_document)
        DocumentType.OTHER -> stringResource(R.string.doc_other)
    }
}

internal fun formatFileSize(bytes: Long): String {
    return when {
        bytes < 1024 -> "$bytes B"
        bytes < 1024 * 1024 -> "${bytes / 1024} KB"
        else -> String.format("%.1f MB", bytes / (1024.0 * 1024.0))
    }
}

internal fun formatUploadDate(dateStr: String): String {
    return try {
        // Handle ISO format like "2024-01-15T10:30:00"
        val parts = dateStr.split("T")
        if (parts.isNotEmpty()) {
            val dateParts = parts[0].split("-")
            if (dateParts.size == 3) {
                "${dateParts[2]}.${dateParts[1]}.${dateParts[0]}"
            } else {
                dateStr
            }
        } else {
            dateStr
        }
    } catch (_: Exception) {
        dateStr
    }
}
