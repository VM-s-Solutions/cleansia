package cz.cleansia.partner.features.orders

import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.CurrencyListItem
import cz.cleansia.partner.api.model.HomeDistanceBand
import cz.cleansia.partner.api.model.OrderAddress
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.PackageListItem
import cz.cleansia.partner.api.model.ServiceListItem

/**
 * Address and extra-slug helpers used across the order-details sub-components.
 * Order date/time/money formatting is shared with the customer app via
 * [cz.cleansia.core.format].
 */

internal fun OrderAddress?.formatSingleLine(): String? {
    if (this == null) return null
    val parts = listOfNotNull(
        street?.takeIf { it.isNotBlank() },
        city?.takeIf { it.isNotBlank() },
        zipCode?.takeIf { it.isNotBlank() },
    )
    return parts.joinToString(", ").takeIf { it.isNotBlank() }
}

/**
 * Human-readable name for a known extra slug. Partner-app doesn't fetch
 * the extras catalog (the list cards only show a "+N" count), so we keep
 * names in step with the seed data (`insert_seed_data.sql §7b`) — and the
 * seed already carries all five locales, so the names are string resources
 * rather than the English literals they used to be. The wire sends bare
 * slugs and nothing else, which is why a Czech cleaner was reading "Inside
 * oven cleaning" on an otherwise Czech screen.
 *
 * Unknown slugs fall back to the slug with dashes turned into spaces +
 * title-cased — untranslated, but readable, and it keeps a newly seeded
 * extra from rendering as nothing until the next release.
 */
@Composable
internal fun nameForExtraSlug(slug: String): String = when (slug) {
    "inside-oven" -> stringResource(R.string.extra_name_inside_oven)
    "inside-fridge" -> stringResource(R.string.extra_name_inside_fridge)
    "interior-windows" -> stringResource(R.string.extra_name_interior_windows)
    "laundry-ironing" -> stringResource(R.string.extra_name_laundry_ironing)
    "pet-hair-supplement" -> stringResource(R.string.extra_name_pet_hair_supplement)
    else -> slug.replace('-', ' ').replaceFirstChar { it.uppercase() }
}

/**
 * The detail's job as the board's row, so the head, the day line, the scope and the clash check run the board's own
 * rules. The band is the board's alone: `OrderItem` carries none.
 */
internal fun OrderItem.asBoardRow(band: HomeDistanceBand? = null): OrderListItem = (orderLocation() as? OrderLocation.Precise).let { precise ->
    OrderListItem(
        id = id,
        displayOrderNumber = displayOrderNumber,
        customerName = customerName,
        customerPhone = customerPhone,
        customerAddress = precise?.line,
        customerAddressApproximate = customerAddressApproximate,
        customerAddressLatitude = precise?.latitude,
        customerAddressLongitude = precise?.longitude,
        rooms = rooms,
        bathrooms = bathrooms,
        extras = extras,
        cleaningDateTime = cleaningDateTime,
        paymentType = paymentType,
        paymentStatus = paymentStatus,
        amountDueOnCard = amountDueOnCard,
        estimatedTime = estimatedTime,
        orderStatus = orderStatus,
        selectedPackages = selectedPackages?.map { PackageListItem(id = it.id, name = it.name, translations = it.translations) },
        selectedServices = selectedServices?.map { ServiceListItem(id = it.id, name = it.name, translations = it.translations) },
        currency = currency?.let { CurrencyListItem(id = it.id, code = it.code, symbol = it.symbol, name = it.name) },
        requiredEmployees = requiredEmployees,
        assignedEmployeesCount = assignedEmployeesCount,
        availableSpots = availableSpots,
        hasAvailableSpots = hasAvailableSpots,
        estimatedCleanerPay = estimatedCleanerPay,
        dirtinessLevel = dirtinessLevel,
        homeDistanceBand = band,
    )
}
