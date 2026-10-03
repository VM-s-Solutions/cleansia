package cz.cleansia.customer.features.booking

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.indication
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.CleaningServices
import androidx.compose.material.icons.outlined.LocalLaundryService
import androidx.compose.material.icons.outlined.Pets
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.Remove
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Spa
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.ripple
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.CompositingStrategy
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.PropertySize
import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.ui.components.adjustableStepper
import cz.cleansia.customer.ui.components.rememberStepperTick
import cz.cleansia.customer.ui.theme.selectionTint
import cz.cleansia.customer.ui.theme.Sky600

// Local palette for backend-driven categories. Keyed by slug so backend can add
// new categories without code changes; unknown slugs fall back to DefaultPalette.
internal data class CategoryPalette(val icon: ImageVector, val tint: Color)

private val CategoryPalettes = mapOf(
    "home" to CategoryPalette(Icons.Outlined.CleaningServices, Color(0xFF0284C7)),
    "deep" to CategoryPalette(Icons.Outlined.Spa, Color(0xFF7C3AED)),
    "laundry" to CategoryPalette(Icons.Outlined.LocalLaundryService, Color(0xFF0891B2)),
    "pet" to CategoryPalette(Icons.Outlined.Pets, Color(0xFFEA580C)),
)

private val DefaultPalette = CategoryPalette(Icons.Outlined.Star, Color(0xFF0284C7))

internal fun CategoryDto.palette(): CategoryPalette = CategoryPalettes[slug] ?: DefaultPalette
internal fun CategoryPalette.bg() = tint.copy(alpha = 0.12f)

// Package cards keep their gradient tiers — cycle through the three brand
// gradients by package index so the UI still feels layered without backend-side
// metadata for tagline/accent.
private enum class PackageAccent { Blue, Purple, Cyan }

@Composable
private fun gradientFor(accent: PackageAccent): List<Color> = when (accent) {
    PackageAccent.Blue -> cz.cleansia.customer.ui.theme.BrandGradients.blue()
    PackageAccent.Purple -> cz.cleansia.customer.ui.theme.BrandGradients.purple()
    PackageAccent.Cyan -> cz.cleansia.customer.ui.theme.BrandGradients.cyan()
}.let { (a, b) -> listOf(a, b) }

private fun accentForIndex(idx: Int): PackageAccent = when (idx % 3) {
    0 -> PackageAccent.Blue
    1 -> PackageAccent.Purple
    else -> PackageAccent.Cyan
}

/**
 * The catalog arrives in one language with a per-language [TranslationDto] map
 * beside it, so every render of a service, package or category name goes through
 * here. Split from the `@Composable` wrapper below so the fallback chain is
 * assertable without a composition.
 */
internal fun pickTranslatedName(
    translations: Map<String, cz.cleansia.customer.core.catalog.TranslationDto>?,
    lang: String?,
    fallback: String,
): String {
    if (lang.isNullOrBlank() || translations.isNullOrEmpty()) return fallback
    return translations[lang]?.name ?: fallback
}

internal fun pickTranslatedDescription(
    translations: Map<String, cz.cleansia.customer.core.catalog.TranslationDto>?,
    lang: String?,
    fallback: String?,
): String? {
    if (lang.isNullOrBlank() || translations.isNullOrEmpty()) return fallback
    return translations[lang]?.description ?: fallback
}

/**
 * Active locale comes from the Configuration, which honours a per-app language
 * override, so the catalog follows the app's language rather than the device's.
 */
@Composable
internal fun localizedName(
    translations: Map<String, cz.cleansia.customer.core.catalog.TranslationDto>?,
    fallback: String,
): String = pickTranslatedName(translations, LocalConfiguration.current.locales.get(0)?.language, fallback)

@Composable
internal fun localizedDescription(
    translations: Map<String, cz.cleansia.customer.core.catalog.TranslationDto>?,
    fallback: String?,
): String? = pickTranslatedDescription(translations, LocalConfiguration.current.locales.get(0)?.language, fallback)

@Composable
fun ServicesStep(
    state: BookingState,
    onToggleService: (String) -> Unit,
    /** True when the selection changed now; false when the customer is asked to confirm first. */
    onTogglePackage: (String) -> Boolean,
    onRoomsChange: (Int) -> Unit,
    onBathroomsChange: (Int) -> Unit,
    doubleBooking: DoubleBooking?,
    onConfirmDoubleBooking: () -> Unit,
    onDismissDoubleBooking: () -> Unit,
    viewModel: ServicesStepViewModel = androidx.hilt.navigation.compose.hiltViewModel(),
) {
    val catalogRepo = viewModel.catalogRepository

    val services by catalogRepo.services.collectAsState()
    val packages by catalogRepo.packages.collectAsState()
    val loading by catalogRepo.loading.collectAsState()
    val loaded by catalogRepo.loaded.collectAsState()
    val currencyCode by catalogRepo.currencyCode.collectAsState()

    // Distinct categories derived from the loaded services, sorted by backend
    // displayOrder. Only service categories — packages stay unfiltered.
    val categories = remember(services) {
        services.map { it.category }.distinctBy { it.slug }.sortedBy { it.displayOrder }
    }

    // null = synthetic "All" chip. Only filters the services list.
    var activeCategorySlug by remember { mutableStateOf<String?>(null) }

    val filteredServices = remember(services, activeCategorySlug) {
        if (activeCategorySlug == null) services
        else services.filter { it.category.slug == activeCategorySlug }
    }

    // Details-sheet state. Exactly one of these is non-null at a time; dismissing
    // either resets both. Kept at step level so the sheet composes over the full
    // ServicesStep surface, not just inside a single row/card.
    var detailService by remember { mutableStateOf<ServiceListItem?>(null) }
    var detailPackage by remember { mutableStateOf<PackageListItem?>(null) }

    when {
        loading && !loaded -> LoadingState()
        !loaded -> ErrorState(onRetry = viewModel::refreshCatalog)
        services.isEmpty() && packages.isEmpty() -> EmptyCatalogState(
            onRetry = viewModel::refreshCatalog,
        )
        else -> CatalogContent(
            state = state,
            onToggleService = onToggleService,
            onRoomsChange = onRoomsChange,
            onBathroomsChange = onBathroomsChange,
            services = services,
            packages = packages,
            currencyCode = currencyCode,
            categories = categories,
            activeCategorySlug = activeCategorySlug,
            onCategoryChange = { activeCategorySlug = it },
            filteredServices = filteredServices,
            onServiceInfo = { detailService = it },
            onPackageOpen = { detailPackage = it },
        )
    }

    detailService?.let { svc ->
        ServiceDetailsSheet(
            service = svc,
            currencyCode = currencyCode,
            onDismiss = { detailService = null },
        )
    }

    detailPackage?.let { pkg ->
        val selected = state.selectedPackageIds.contains(pkg.id)
        PackageDetailsSheet(
            pkg = pkg,
            currencyCode = currencyCode,
            isSelected = selected,
            onToggle = { if (onTogglePackage(pkg.id)) detailPackage = null },
            onDismiss = { detailPackage = null },
        )
    }

    // Over the package sheet when its add asks: the sheet closes once the package is in, and stays
    // open on Cancel.
    doubleBooking?.let { pick ->
        DoubleBookingDialog(
            pick = pick,
            onConfirm = {
                onConfirmDoubleBooking()
                detailPackage = null
            },
            onDismiss = onDismissDoubleBooking,
        )
    }
}

@Composable
private fun CatalogContent(
    state: BookingState,
    onToggleService: (String) -> Unit,
    onRoomsChange: (Int) -> Unit,
    onBathroomsChange: (Int) -> Unit,
    services: List<ServiceListItem>,
    packages: List<PackageListItem>,
    currencyCode: String?,
    categories: List<CategoryDto>,
    activeCategorySlug: String?,
    onCategoryChange: (String?) -> Unit,
    filteredServices: List<ServiceListItem>,
    onServiceInfo: (ServiceListItem) -> Unit,
    onPackageOpen: (PackageListItem) -> Unit,
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(vertical = 12.dp),
    ) {
        item {
            Column(Modifier.padding(horizontal = 20.dp)) {
                PropertyCompactRow(
                    rooms = state.rooms,
                    bathrooms = state.bathrooms,
                    onRoomsChange = onRoomsChange,
                    onBathroomsChange = onBathroomsChange,
                )
            }
            Spacer(Modifier.height(20.dp))
        }

        if (packages.isNotEmpty()) {
            item {
                SectionHeader(
                    stringResource(R.string.booking_packages_featured),
                    Modifier.padding(horizontal = 20.dp),
                )
                Spacer(Modifier.height(10.dp))
                LazyRow(
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 20.dp),
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    itemsIndexed(packages) { idx, pkg ->
                        val selected = state.selectedPackageIds.contains(pkg.id)
                        PackageCard(
                            pkg = pkg,
                            currencyCode = currencyCode,
                            accent = accentForIndex(idx),
                            selected = selected,
                            onClick = { onPackageOpen(pkg) },
                        )
                    }
                }
                Spacer(Modifier.height(24.dp))
            }
        }

        if (services.isEmpty()) {
            item { EmptyResults() }
        } else {
            item {
                SectionHeader(
                    stringResource(R.string.booking_pick_service),
                    Modifier.padding(horizontal = 20.dp),
                )
                Spacer(Modifier.height(10.dp))
                // Chip row only when there's more than one category to choose from —
                // a single-category catalog doesn't need a filter.
                if (categories.size > 1) {
                    LazyRow(
                        contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 20.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        item {
                            CategoryChip(
                                label = stringResource(R.string.booking_cat_all),
                                icon = Icons.Outlined.Star,
                                tint = Color(0xFF0284C7),
                                selected = activeCategorySlug == null,
                                onClick = { onCategoryChange(null) },
                            )
                        }
                        items(categories) { cat ->
                            val palette = cat.palette()
                            CategoryChip(
                                label = localizedName(cat.translations, cat.name),
                                icon = palette.icon,
                                tint = palette.tint,
                                selected = activeCategorySlug == cat.slug,
                                onClick = { onCategoryChange(cat.slug) },
                            )
                        }
                    }
                    Spacer(Modifier.height(12.dp))
                }
            }
            if (filteredServices.isEmpty()) {
                item { EmptyResults() }
            }
            items(filteredServices) { service ->
                Column(Modifier.padding(horizontal = 20.dp)) {
                    ServiceRow(
                        service = service,
                        currencyCode = currencyCode,
                        selected = state.selectedServiceIds.contains(service.id),
                        inPackages = packages.selectedIncluding(service.id, state.selectedPackageIds),
                        onInfoClick = { onServiceInfo(service) },
                    ) { onToggleService(service.id) }
                    Spacer(Modifier.height(8.dp))
                }
            }
        }

        item { Spacer(Modifier.height(32.dp)) }
    }
}

/* ── Package card — featured, gradient, premium feel ── */

@Composable
private fun PackageCard(
    pkg: PackageListItem,
    currencyCode: String?,
    accent: PackageAccent,
    selected: Boolean,
    onClick: () -> Unit,
) {
    val gradient = gradientFor(accent)
    val name = localizedName(pkg.translations, pkg.name)
    val description = localizedDescription(pkg.translations, pkg.description)
    val included = pkg.includedServices.orEmpty()
    val includesSummary = buildIncludesSummary(included)
    Box(
        modifier = Modifier
            .width(260.dp)
            .height(158.dp)
            .clip(RoundedCornerShape(20.dp))
            .background(Brush.linearGradient(gradient))
            .clickable(onClick = onClick)
            .padding(14.dp),
    ) {
        if (selected) {
            Box(
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .size(26.dp)
                    .background(Color.White, CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    Icons.Outlined.Check,
                    contentDescription = null,
                    tint = gradient.first(),
                    modifier = Modifier.size(16.dp),
                )
            }
        }

        Column(modifier = Modifier.fillMaxSize()) {
            Text(
                name,
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = Color.White,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                // Leave room for the selected-check badge in the top-right corner.
                modifier = Modifier.padding(end = if (selected) 32.dp else 0.dp),
            )

            if (!description.isNullOrBlank()) {
                Spacer(Modifier.height(2.dp))
                FadingDescription(
                    text = description,
                    fadeColor = gradient.last(),
                )
            }

            if (includesSummary != null) {
                Spacer(Modifier.height(6.dp))
                Text(
                    text = includesSummary,
                    style = MaterialTheme.typography.bodySmall,
                    color = Color.White.copy(alpha = 0.85f),
                    // Single line keeps the smaller card from looking crowded.
                    // Full list still lives in the details bottom sheet.
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }

            Spacer(Modifier.weight(1f).heightIn(min = 4.dp))

            Text(
                formatOrderPrice(pkg.price, pkg.currencyCode ?: currencyCode),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = Color.White,
            )
        }
    }
}

/**
 * Single-line summary of what's inside the package — shown in the card footer
 * so users can decide at a glance. Full list lives in the details bottom sheet.
 *
 * Examples (localization via the caller's [localizedName] on each service):
 *   - 1 service  → "Includes: Deep Cleaning"
 *   - 2 services → "Includes: Deep Cleaning, Kitchen Cleaning"
 *   - 3+         → "Includes: Deep Cleaning, Kitchen Cleaning + 2 more"
 */
@Composable
private fun buildIncludesSummary(included: List<cz.cleansia.customer.core.catalog.PackageServiceSummary>): String? {
    if (included.isEmpty()) return null
    val names = included.map { localizedName(it.translations, it.name) }
    val prefix = stringResource(R.string.booking_package_includes_prefix)
    return when {
        names.size <= 2 -> "$prefix ${names.joinToString(", ")}"
        else -> "$prefix ${names.take(2).joinToString(", ")} " +
            pluralStringResource(R.plurals.booking_package_more, names.size - 2, names.size - 2)
    }
}

/**
 * One-line description that fades to the card's gradient color on the right
 * edge instead of showing a hard ellipsis — subtle cue that there's more to
 * read if the user taps for details.
 */
@Composable
private fun FadingDescription(text: String, fadeColor: Color) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .graphicsLayer(compositingStrategy = CompositingStrategy.Offscreen)
            .drawWithContent {
                drawContent()
                val fadeWidth = 36.dp.toPx().coerceAtMost(size.width)
                drawRect(
                    brush = Brush.horizontalGradient(
                        colors = listOf(Color.Transparent, fadeColor),
                        startX = size.width - fadeWidth,
                        endX = size.width,
                    ),
                    blendMode = BlendMode.SrcAtop,
                )
            },
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.bodySmall,
            color = Color.White.copy(alpha = 0.9f),
            maxLines = 1,
            softWrap = false,
            overflow = TextOverflow.Clip,
        )
    }
}

/* ── Category chip — filters the services list only ── */

@Composable
private fun CategoryChip(
    label: String,
    icon: ImageVector,
    tint: Color,
    selected: Boolean,
    onClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(if (selected) tint else MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                if (selected) tint else MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(999.dp),
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            icon,
            null,
            tint = if (selected) Color.White else tint,
            modifier = Modifier.size(14.dp),
        )
        Spacer(Modifier.width(4.dp))
        Text(
            label,
            style = MaterialTheme.typography.labelLarge.copy(
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
            ),
            color = if (selected) Color.White else MaterialTheme.colorScheme.onSurface,
        )
    }
}

/* ── Service row — cleaner, color-coded ── */

@Composable
private fun ServiceRow(
    service: ServiceListItem,
    currencyCode: String?,
    selected: Boolean,
    inPackages: List<PackageListItem>,
    onInfoClick: () -> Unit,
    onClick: () -> Unit,
) {
    val palette = service.category.palette()
    val name = localizedName(service.translations, service.name)
    val description = localizedDescription(service.translations, service.description)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(if (selected) selectionTint() else MaterialTheme.colorScheme.surface)
            .border(
                width = if (selected) 2.dp else 1.dp,
                color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            )
            .clickable(onClick = onClick)
            .padding(14.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Box(
            Modifier.size(44.dp).background(palette.bg(), RoundedCornerShape(12.dp)),
            contentAlignment = Alignment.Center,
        ) { Icon(palette.icon, null, tint = palette.tint, modifier = Modifier.size(22.dp)) }
        Spacer(Modifier.width(12.dp))

        Column(Modifier.weight(1f)) {
            Text(
                name,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            InPackageMarker(inPackages)
            if (!description.isNullOrBlank()) {
                Spacer(Modifier.height(4.dp))
                Text(
                    description,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            Spacer(Modifier.height(8.dp))
            val rowCurrency = service.currencyCode ?: currencyCode
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    stringResource(R.string.booking_price_from, formatOrderPrice(service.basePrice, rowCurrency)),
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                if (service.perRoomPrice > 0) {
                    Spacer(Modifier.width(6.dp))
                    Text(
                        stringResource(R.string.booking_price_per_room, formatOrderPrice(service.perRoomPrice, rowCurrency)),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        Spacer(Modifier.width(8.dp))
        Column(
            horizontalAlignment = Alignment.End,
            verticalArrangement = Arrangement.spacedBy(6.dp),
        ) {
            // Info icon is always present; tapping bubbles up through onInfoClick
            // independently from the row's select tap. Icon button has a transparent
            // ripple area to feel like a distinct affordance.
            Box(
                modifier = Modifier
                    .size(28.dp)
                    .clip(CircleShape)
                    .clickable(onClick = onInfoClick),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    Icons.Outlined.Info,
                    contentDescription = stringResource(R.string.common_details),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
            if (selected) {
                Box(
                    modifier = Modifier
                        .size(22.dp)
                        .background(MaterialTheme.colorScheme.primary, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(
                        Icons.Outlined.Check,
                        contentDescription = null,
                        tint = Color.White,
                        modifier = Modifier.size(14.dp),
                    )
                }
            }
        }
    }
}

@Composable
private fun PropertyCompactRow(
    rooms: Int,
    bathrooms: Int,
    onRoomsChange: (Int) -> Unit,
    onBathroomsChange: (Int) -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(selectionTint())
            .padding(horizontal = 14.dp, vertical = 10.dp),
    ) {
        // The title sits above the counters, not beside them: on a 360dp phone the two uk counters
        // ("2 кімнати", "1 ванна кімната") need more than the row's width on their own, and beside
        // them the title was squeezed to nothing. A counter that still does not fit wraps its label.
        Text(
            stringResource(R.string.booking_your_home),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
        )
        Spacer(Modifier.height(6.dp))
        // Two equal capsules filling the row with the quick-size slide's 8dp gap, from the title's
        // leading edge to the card's trailing one, and as tall as each other when one label wraps.
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(IntrinsicSize.Min),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            // Both ends of each stepper stop at the bounds, so a tap that cannot move the number never
            // looks like one that can. TalkBack reads each as "Your home" and its value, as iOS does.
            val home = stringResource(R.string.booking_your_home)
            CompactCounter(
                name = home,
                label = pluralStringResource(R.plurals.booking_rooms_short, rooms, rooms),
                value = rooms,
                range = 1..PropertySize.MAX_ROOMS,
                onChange = onRoomsChange,
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight(),
            )
            CompactCounter(
                name = home,
                label = pluralStringResource(R.plurals.booking_bath_short, bathrooms, bathrooms),
                value = bathrooms,
                range = 1..PropertySize.MAX_BATHROOMS,
                onChange = onBathroomsChange,
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight(),
            )
        }
        // The cap stated up front, so the plus stopping at it reads as the rule rather than a bug.
        Spacer(Modifier.height(4.dp))
        Text(
            stringResource(R.string.booking_size_limit_caption, PropertySize.MAX_ROOMS, PropertySize.MAX_BATHROOMS),
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/**
 * The size row's −/+ counter, iOS `PropertyStepper`'s twin: a 28dp pill whose two steps each take a
 * 48dp target, one TalkBack node adjusted like a slider, and a selection tick on every step.
 */
@Composable
private fun CompactCounter(
    name: String,
    label: String,
    value: Int,
    range: IntRange,
    onChange: (Int) -> Unit,
    modifier: Modifier = Modifier,
) {
    val tick = rememberStepperTick()
    val change = { next: Int ->
        tick()
        onChange(next)
    }
    Row(
        modifier = modifier
            // Not clipped: each step's target overhangs the pill, and a clip would stop the overhang
            // taking taps (the quick-size stepper's precedent).
            .background(MaterialTheme.colorScheme.surface, RoundedCornerShape(999.dp))
            .adjustableStepper(name = name, valueText = label, value = value, range = range, onChange = change),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        CounterStep(Icons.Outlined.Remove, outwardStart = true, enabled = value > range.first) { change(value - 1) }
        Text(
            label,
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold, lineHeight = 16.sp),
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
            textAlign = TextAlign.Center,
            // Weighted so both steps are measured first and the label is centred between them: a label
            // wider than the space left wraps rather than pushing the plus out of the pill.
            modifier = Modifier.weight(1f).padding(horizontal = 4.dp),
        )
        CounterStep(Icons.Outlined.Add, outwardStart = false, enabled = value < range.last) { change(value + 1) }
    }
}

/**
 * A 28dp glyph whose target is a 48dp square without growing the pill, as on iOS: 10dp above and
 * below, 16dp toward the label and 4dp outward, so two counters sharing the row split the 8dp
 * between them. The ripple stays on the glyph.
 */
@Composable
private fun CounterStep(icon: ImageVector, outwardStart: Boolean, enabled: Boolean, onClick: () -> Unit) {
    val interactions = remember { MutableInteractionSource() }
    val glyphX = if (outwardStart) StepOutward else StepTarget - StepGlyph - StepOutward
    Box(
        Modifier
            .layout { measurable, _ ->
                val target = StepTarget.roundToPx()
                val glyph = StepGlyph.roundToPx()
                val placeable = measurable.measure(Constraints.fixed(target, target))
                layout(glyph, glyph) { placeable.place(-glyphX.roundToPx(), (glyph - target) / 2) }
            }
            .clickable(interactionSource = interactions, indication = null, enabled = enabled, onClick = onClick),
    ) {
        Box(
            Modifier
                .offset(x = glyphX, y = (StepTarget - StepGlyph) / 2)
                .size(StepGlyph)
                .indication(interactions, ripple(bounded = false, radius = StepGlyph / 2)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                icon,
                null,
                tint = MaterialTheme.colorScheme.primary.copy(alpha = if (enabled) 1f else 0.38f),
                modifier = Modifier.size(14.dp),
            )
        }
    }
}

private val StepGlyph = 28.dp
private val StepTarget = 48.dp
private val StepOutward = 4.dp

@Composable
private fun EmptyResults() {
    Column(
        modifier = Modifier.fillMaxWidth().padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(Icons.Outlined.Search, null, tint = MaterialTheme.colorScheme.outlineVariant, modifier = Modifier.size(48.dp))
        Spacer(Modifier.height(8.dp))
        Text(stringResource(R.string.booking_no_results), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun LoadingState() {
    Column(
        modifier = Modifier.fillMaxSize().padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
        Spacer(Modifier.height(12.dp))
        Text(
            stringResource(R.string.booking_catalog_loading),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun ErrorState(onRetry: () -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize().padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.Refresh,
            null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(40.dp),
        )
        Spacer(Modifier.height(8.dp))
        Text(
            stringResource(R.string.booking_catalog_error),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(12.dp))
        Text(
            stringResource(R.string.booking_catalog_retry),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onRetry)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}

@Composable
private fun EmptyCatalogState(onRetry: () -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize().padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CleaningServices,
            null,
            tint = MaterialTheme.colorScheme.outlineVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(8.dp))
        Text(
            stringResource(R.string.booking_catalog_empty),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(12.dp))
        Text(
            stringResource(R.string.booking_catalog_retry),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onRetry)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}

@Composable
private fun SectionHeader(text: String, modifier: Modifier = Modifier) {
    Text(
        text,
        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
        color = MaterialTheme.colorScheme.onBackground,
        modifier = modifier,
    )
}
