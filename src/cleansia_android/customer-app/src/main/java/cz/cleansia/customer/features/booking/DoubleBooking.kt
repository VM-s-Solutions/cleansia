package cz.cleansia.customer.features.booking

import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.PackageServiceSummary
import cz.cleansia.customer.core.catalog.ServiceListItem

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

/**
 * The line under a service's name while [packages], the chosen ones including it, already book it; nothing
 * for none. Drawn inside the row's clickable, so TalkBack reads it with the row, which stays selectable.
 */
@Composable
fun InPackageMarker(packages: List<PackageListItem>) {
    if (packages.isEmpty()) return
    val names = packages.map { localizedName(it.translations, it.name) }.joinToString(", ")
    Row(modifier = Modifier.padding(top = 2.dp), verticalAlignment = Alignment.CenterVertically) {
        Icon(
            Icons.Filled.Inventory2,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(12.dp),
        )
        Spacer(Modifier.width(4.dp))
        Text(
            stringResource(packages.inPackageMarkerRes, names),
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
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
