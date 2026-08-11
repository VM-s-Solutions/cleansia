package cz.cleansia.partner.features.profile

import cz.cleansia.partner.api.model.MyPayoutDetails

/**
 * Renders a payout destination the way the cleaner's own bank statement does —
 * `prefix-number/bankCode` locally, the IBAN for everything else.
 *
 * The server stores the local parts zero-padded to their fixed widths, so the padding is
 * stripped back off for display and for pre-filling the form; re-padding it is the server's
 * job and is idempotent.
 */
fun payoutAccountSummary(details: MyPayoutDetails?): String? {
    if (details == null) return null

    val number = details.accountNumber.withoutPayoutPadding()
    val bankCode = details.bankCode?.trim().orEmpty()
    if (number.isNotEmpty() && bankCode.isNotEmpty()) {
        val prefix = details.accountPrefix.withoutPayoutPadding()
        return if (prefix.isEmpty()) "$number/$bankCode" else "$prefix-$number/$bankCode"
    }

    return details.iban?.trim()?.ifEmpty { null } ?: number.ifEmpty { null }
}

internal fun String?.withoutPayoutPadding(): String =
    this?.trim()?.dropWhile { it == '0' }.orEmpty()
