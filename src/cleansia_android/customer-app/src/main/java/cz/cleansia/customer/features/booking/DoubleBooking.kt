package cz.cleansia.customer.features.booking

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.PackageServiceSummary
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.ui.theme.isDark

/**
 * A tap that books one service once more: a package and a service it includes, both chosen, or two
 * chosen packages that include the same service. All are kept, so the service is done and charged once
 * per pick. The pick is never refused or merged; the customer confirms it first. Only a tap on the
 * services step or the schedule form asks. A selection seeded from elsewhere (a Home package,
 * quick-size, Order again, a resumed draft, a schedule being edited) never does, and its rows only
 * carry the "In your package" marker.
 * → /product/business-rules#charging-a-package-and-a-service-together
 */
sealed interface DoubleBooking {
    /** Adding [service], which the selected [packages] include. */
    data class Service(val service: ServiceListItem, val packages: List<PackageListItem>) : DoubleBooking {
        /** "Already in your package" beside one package that includes it, "packages" beside two or more. */
        val titleRes: Int
            get() = if (packages.size > 1) R.string.booking_twice_service_title_many else R.string.booking_twice_service_title

        /** Beside one package that includes it the service is booked twice; beside two or more, "twice" is false. */
        val messageRes: Int
            get() = if (packages.size > 1) R.string.booking_twice_service_message_many else R.string.booking_twice_service_message
    }

    /** Adding [pkg], which includes [services], already in the booking on their own or through another chosen package. */
    data class Package(val pkg: PackageListItem, val services: List<PackageServiceSummary>) : DoubleBooking
}

/** The selected packages that include [serviceId]: what the service's row is marked with. */
fun List<PackageListItem>.selectedIncluding(serviceId: String, selectedPackageIds: Set<String>): List<PackageListItem> =
    filter { pkg -> pkg.id in selectedPackageIds && pkg.includedServices.orEmpty().any { it.serviceId == serviceId } }

/** The marker's line for these chosen packages: "In your package: …" for one, "In your packages: …" for two or more. */
val List<PackageListItem>.inPackageMarkerRes: Int
    get() = if (size > 1) R.string.booking_in_your_packages else R.string.booking_in_your_package

/** What adding the service [serviceId] would book once more, or null when it books nothing again. */
fun doubleBookingOfService(
    serviceId: String,
    selectedPackageIds: Set<String>,
    services: List<ServiceListItem>,
    packages: List<PackageListItem>,
): DoubleBooking.Service? {
    val service = services.firstOrNull { it.id == serviceId } ?: return null
    val holders = packages.selectedIncluding(serviceId, selectedPackageIds)
    return if (holders.isEmpty()) null else DoubleBooking.Service(service, holders)
}

/**
 * What adding the package [packageId] would book once more, or null when it books nothing again: the
 * services it includes that are already in the booking, chosen on their own or through another chosen
 * package, in the package's own order.
 */
fun doubleBookingOfPackage(
    packageId: String,
    selectedServiceIds: Set<String>,
    selectedPackageIds: Set<String>,
    packages: List<PackageListItem>,
): DoubleBooking.Package? {
    val pkg = packages.firstOrNull { it.id == packageId } ?: return null
    val throughPackages = packages
        .filter { it.id != packageId && it.id in selectedPackageIds }
        .flatMap { other -> other.includedServices.orEmpty().mapNotNull { it.serviceId } }
    val inBooking = selectedServiceIds + throughPackages
    val again = pkg.includedServices.orEmpty().filter { it.serviceId != null && it.serviceId in inBooking }
    return if (again.isEmpty()) null else DoubleBooking.Package(pkg, again)
}

/*
 * A service a chosen package already books reads as covered at a glance, the same on every client: its
 * row takes the brand primary over the card with a primary border ([inPackageRowFill], [inPackageRowBorder]),
 * and [InPackageMarker] is a badge. A picked row keeps its picked look and still carries the badge.
 */

/** How much of the brand primary a covered row's card takes; a dark card needs more to show it. */
fun inPackageRowAlpha(dark: Boolean): Float = if (dark) 0.16f else 0.08f

/** How much of the brand primary the badge lays over its row. */
fun inPackageBadgeAlpha(dark: Boolean): Float = if (dark) 0.24f else 0.14f

const val IN_PACKAGE_BORDER_ALPHA = 0.6f

/** The card of a covered row that is not picked: the brand primary over the surface. */
@Composable
@ReadOnlyComposable
fun inPackageRowFill(): Color = MaterialTheme.colorScheme.primary
    .copy(alpha = inPackageRowAlpha(isDark()))
    .compositeOver(MaterialTheme.colorScheme.surface)

/** The border of a covered row that is not picked: 1.5dp of the brand primary at 60 %. */
@Composable
@ReadOnlyComposable
fun inPackageRowBorder(): BorderStroke =
    BorderStroke(1.5.dp, MaterialTheme.colorScheme.primary.copy(alpha = IN_PACKAGE_BORDER_ALPHA))

/**
 * The badge under a service's name while [packages], the chosen ones including it, already book it; nothing
 * for none. Its check and text take the primary container's ink (sky-900 / sky-100), as iOS's do: the
 * brand primary measures about 3.1:1 over the badge, under the 4.5:1 its text needs. Drawn inside the
 * row's clickable, so TalkBack reads it with the row, which stays selectable.
 */
@Composable
fun InPackageMarker(packages: List<PackageListItem>) {
    if (packages.isEmpty()) return
    val names = packages.map { localizedName(it.translations, it.name) }.joinToString(", ")
    Row(
        modifier = Modifier
            .padding(top = 4.dp)
            // A capsule on one line, a rounded box when a long package name wraps it to two.
            .background(
                MaterialTheme.colorScheme.primary.copy(alpha = inPackageBadgeAlpha(isDark())),
                RoundedCornerShape(12.dp),
            )
            .padding(horizontal = 8.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            Icons.Filled.CheckCircle,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onPrimaryContainer,
            modifier = Modifier.size(14.dp),
        )
        Spacer(Modifier.width(4.dp))
        Text(
            stringResource(packages.inPackageMarkerRes, names),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onPrimaryContainer,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
        )
    }
}

/** The confirm a [DoubleBooking] tap waits for. Cancel is the dismissal, so Back and a tap outside cancel too. */
@Composable
fun DoubleBookingDialog(pick: DoubleBooking, onConfirm: () -> Unit, onDismiss: () -> Unit) {
    val title: String
    val text: String
    val confirm: String
    when (pick) {
        is DoubleBooking.Service -> {
            title = stringResource(pick.titleRes)
            text = stringResource(
                pick.messageRes,
                localizedName(pick.service.translations, pick.service.name),
                pick.packages.map { localizedName(it.translations, it.name) }.joinToString(", "),
            )
            confirm = stringResource(R.string.booking_twice_service_confirm)
        }
        is DoubleBooking.Package -> {
            title = stringResource(R.string.booking_twice_package_title)
            text = stringResource(
                R.string.booking_twice_package_message,
                localizedName(pick.pkg.translations, pick.pkg.name),
                pick.services.map { localizedName(it.translations, it.name) }.joinToString(", "),
            )
            confirm = stringResource(R.string.booking_twice_package_confirm)
        }
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = { Text(text) },
        confirmButton = { TextButton(onClick = onConfirm) { Text(confirm) } },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.common_cancel)) } },
    )
}
