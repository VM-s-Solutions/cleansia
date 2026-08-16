package cz.cleansia.customer.features.booking

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.AccessTime
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.ConfirmationNumber
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.VerifiedUser
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.features.orders.roomsAndBathrooms
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.selectionTint
import cz.cleansia.customer.ui.theme.SuccessText

@Composable
fun ConfirmStep(
    state: BookingState,
    onUpdate: (BookingState) -> Unit,
    viewModel: ConfirmStepViewModel = androidx.hilt.navigation.compose.hiltViewModel(),
) {
    val catalogRepo = viewModel.catalogRepository
    val services by catalogRepo.services.collectAsState()
    val packages by catalogRepo.packages.collectAsState()
    val extras by catalogRepo.extras.collectAsState()

    val selectedServices = remember(services, state.selectedServiceIds) {
        services.filter { it.id in state.selectedServiceIds }
    }
    val selectedPackages = remember(packages, state.selectedPackageIds) {
        packages.filter { it.id in state.selectedPackageIds }
    }
    // Sort by displayOrder so the catalog admin's intended ordering is what
    // the user sees. The repo doesn't pre-sort because other consumers might
    // want a different order.
    val sortedExtras = remember(extras) { extras.sortedBy { it.displayOrder } }

    // Live quote via parent VM — drives the authoritative base price; fall back
    // to a rough catalog sum until the first quote lands so the card isn't blank.
    val bookingVm: BookingViewModel = androidx.hilt.navigation.compose.hiltViewModel()
    // Wave 4 — sealed [QuoteState] replaces nullable quote. Unwrap once so the
    // catalog-sum fallback below keeps its existing single null-coalesce shape.
    val quoteState by bookingVm.quoteState.collectAsStateWithLifecycle()
    val quote = (quoteState as? QuoteState.Quoted)?.response
    val promoState by bookingVm.promoCodeState.collectAsStateWithLifecycle()
    // Every money row on this screen renders through [formatOrderPrice] with the
    // quote's own currency, so the summary and the sheet footer can never disagree.
    // Null before the first quote lands — formatOrderPrice falls back to CZK, which
    // is what the hardcoded " CZK" suffix this replaced always assumed.
    val currencyCode = quote?.currencyCode
    val effectiveDiscount by bookingVm.effectiveDiscount.collectAsStateWithLifecycle()
    // Every money row comes from the one resolver, so this card and the sticky bar below it cannot
    // disagree with each other or with the total the order is created with.
    val summary = BookingPriceSummary.resolve(quote, effectiveDiscount)
    // Catalog sum only until the first quote lands, so the card isn't blank; never a money decision.
    val subtotal = quote?.let { summary.subtotal }
        ?: (selectedServices.sumOf { it.basePrice + it.perRoomPrice * (state.rooms + state.bathrooms) } +
            selectedPackages.sumOf { it.price })
    // LOY-003 — server returns Plus AND tier additively (both can be non-zero on the same quote,
    // already capped at 12% combined). Promo replaces the additive pair if larger; never stacks.
    val tierDiscount = quote?.tierDiscountAmount ?: 0.0
    val membershipDiscount = quote?.membershipDiscountAmount ?: 0.0
    val promoDiscount = (promoState as? PromoCodeUiState.Valid)?.discountAmount ?: 0.0
    val combinedServerDiscount = tierDiscount + membershipDiscount
    // Promo wins → show only the promo line. Otherwise show whichever of
    // (Plus, tier) is non-zero; both can be shown simultaneously now.
    val showPromoLine = promoDiscount > 0.0 && promoDiscount > combinedServerDiscount
    val showMembershipLine = !showPromoLine && membershipDiscount > 0.0
    val showTierLine = !showPromoLine && tierDiscount > 0.0

    // Local sheet-open flags. The applied codes themselves live on BookingState
    // (via the VM), so reopening the sheets re-seeds with the canonical value.
    var promoSheetOpen by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(20.dp),
    ) {
        // ── Extras ──
        // Render only when the backend served some extras. New deploys
        // without seed data, or env where the endpoint failed, see no card
        // (CatalogRepository made the call best-effort, leaves the flow empty).
        if (sortedExtras.isNotEmpty()) {
            ExtrasCard(
                extras = sortedExtras,
                selectedSlugs = state.selectedExtraSlugs,
                currencyCode = currencyCode,
                onToggle = { slug ->
                    val updated = if (slug in state.selectedExtraSlugs) {
                        state.selectedExtraSlugs - slug
                    } else {
                        state.selectedExtraSlugs + slug
                    }
                    onUpdate(state.copy(selectedExtraSlugs = updated))
                },
            )
            Spacer(Modifier.height(16.dp))
        }

        // ── Order summary ──
        SummaryCard {
            // Items the customer picked, with per-row prices. Header gives the
            // section a clear visual anchor instead of bare rows.
            Text(
                stringResource(R.string.booking_summary_items_label),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            selectedServices.forEach { svc ->
                Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    // Same [localizedName] every other catalog render uses — the
                    // summary must name the item exactly as the picker did.
                    Text(localizedName(svc.translations, svc.name), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                    Text(
                        formatOrderPrice(
                            svc.basePrice + svc.perRoomPrice * (state.rooms + state.bathrooms),
                            currencyCode,
                        ),
                        style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
            selectedPackages.forEach { pkg ->
                Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(localizedName(pkg.translations, pkg.name), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                    Text(
                        formatOrderPrice(pkg.price, currencyCode),
                        style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
            HorizontalDivider(Modifier.padding(vertical = 10.dp))

            // Booking details with explicit labels — easier to scan than bare icons.
            Text(
                stringResource(R.string.booking_summary_details_label),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(4.dp))
            LabeledInfoRow(Icons.Outlined.LocationOn, stringResource(R.string.booking_summary_address), state.street.ifBlank { "—" })
            LabeledInfoRow(
                Icons.Outlined.Home,
                stringResource(R.string.booking_summary_property),
                roomsAndBathrooms(state.rooms, state.bathrooms),
            )
            LabeledInfoRow(Icons.Outlined.CalendarToday, stringResource(R.string.booking_summary_date), state.selectedDate.ifBlank { "—" })
            LabeledInfoRow(Icons.Outlined.AccessTime, stringResource(R.string.booking_summary_time), state.selectedTime.ifBlank { "—" })

            HorizontalDivider(Modifier.padding(vertical = 10.dp))

            // Total breakdown — subtotal + (optional) express surcharge + grand total, every figure
            // split out of the server quote so the rows add up to the number that gets charged.
            Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(
                    stringResource(R.string.booking_summary_subtotal),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    formatOrderPrice(subtotal, currencyCode),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            if (showPromoLine) {
                Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        stringResource(R.string.booking_summary_promo_discount, state.promoCode.trim().uppercase()),
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                    Text(
                        "-${formatOrderPrice(promoDiscount, currencyCode)}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                }
            }
            if (showMembershipLine) {
                Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        stringResource(R.string.booking_summary_membership_discount),
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                    Text(
                        "-${formatOrderPrice(membershipDiscount, currencyCode)}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                }
            }
            if (showTierLine) {
                Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        stringResource(R.string.booking_summary_tier_discount),
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                    Text(
                        "-${formatOrderPrice(tierDiscount, currencyCode)}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                }
            }
            if (summary.expressLine == BookingPriceSummary.ExpressLine.Charged) {
                Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        stringResource(R.string.booking_summary_express_surcharge),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        "+${formatOrderPrice(summary.expressSurcharge, currencyCode)}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
            if (summary.expressLine == BookingPriceSummary.ExpressLine.Waived) {
                Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        stringResource(R.string.booking_summary_express_surcharge_waived),
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                    Text(
                        formatOrderPrice(0.0, currencyCode),
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                }
            }
            // "Didn't apply" hint — surfaces when the user has a tier benefit but
            // the order falls below the per-tier minimum. Only show when no
            // discount is currently winning, otherwise it's misleading noise.
            val tierFloor = quote?.tierDiscountMinOrderAmount
            if (effectiveDiscount == 0.0 && tierFloor != null && tierFloor > 0.0 && subtotal < tierFloor) {
                Text(
                    stringResource(
                        R.string.booking_summary_tier_discount_min_not_met,
                        formatOrderPrice(tierFloor, currencyCode),
                    ),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
            Spacer(Modifier.height(6.dp))
            Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(
                    stringResource(R.string.booking_summary_total),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Text(
                    formatOrderPrice(summary.total, currencyCode),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.primary,
                )
            }
        }
        Spacer(Modifier.height(16.dp))

        // ── Promo code (Loyalty Phase B) — Wolt-style row + dialog ──
        // Tap the row → modal sheet → user types + Apply → backend call → applied
        // code persists in BookingState, summary line above re-renders.
        PromoCodeRow(
            appliedCode = state.promoCode.takeIf { promoState is PromoCodeUiState.Valid }.orEmpty(),
            onClick = { promoSheetOpen = true },
            onClear = { bookingVm.clearPromoCode() },
        )
        Spacer(Modifier.height(12.dp))

        // (Referral codes are signup-only — removed from the booking flow on
        // purpose. One-per-invitee is enforced at registration; a separate
        // entry here just confused users who'd already redeemed at signup.)
        Spacer(Modifier.height(16.dp))

        // ── Payment method ──
        SectionLabel(stringResource(R.string.booking_payment_method))
        Spacer(Modifier.height(10.dp))

        PaymentOption(
            icon = Icons.Outlined.CreditCard,
            title = stringResource(R.string.booking_pay_card),
            subtitle = stringResource(R.string.booking_pay_card_desc),
            selected = state.paymentMethod == "card",
            onClick = { onUpdate(state.copy(paymentMethod = "card")) },
        )
        Spacer(Modifier.height(8.dp))
        PaymentOption(
            icon = Icons.Outlined.Payments,
            title = stringResource(R.string.booking_pay_cash),
            subtitle = stringResource(R.string.booking_pay_cash_desc),
            selected = state.paymentMethod == "cash",
            onClick = { onUpdate(state.copy(paymentMethod = "cash")) },
        )

        Spacer(Modifier.height(16.dp))

        InstructionsFields(
            specialInstructions = state.specialInstructions,
            accessInstructions = state.accessInstructions,
            onSpecialInstructionsChange = { onUpdate(state.copy(specialInstructions = it)) },
            onAccessInstructionsChange = bookingVm::updateAccessInstructions,
        )

        Spacer(Modifier.height(16.dp))

        // ── Plus: pre-request a favorite cleaner ──
        // Renders nothing for non-Plus users or when the user has no eligible
        // cleaners — see PreferredCleanerPicker for the gating.
        PreferredCleanerPicker(
            selectedEmployeeId = state.preferredEmployeeId,
            onSelect = { id, _ -> onUpdate(state.copy(preferredEmployeeId = id)) },
        )

        Spacer(Modifier.height(20.dp))

        // ── Cancellation policy ──
        CancellationPolicyCard(membershipRepository = viewModel.membershipRepository)
        Spacer(Modifier.height(16.dp))

        // ── Trust badges ──
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(14.dp))
                .background(MaterialTheme.colorScheme.surface)
                .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
                .padding(14.dp)
                .height(androidx.compose.foundation.layout.IntrinsicSize.Max),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            TrustBadge(
                Icons.Outlined.Shield,
                stringResource(R.string.booking_trust_insured),
                Modifier.weight(1f).fillMaxHeight(),
            )
            Box(Modifier.width(1.dp).fillMaxHeight().background(MaterialTheme.colorScheme.outlineVariant))
            TrustBadge(
                Icons.Outlined.VerifiedUser,
                stringResource(R.string.booking_trust_vetted),
                Modifier.weight(1f).fillMaxHeight(),
            )
        }

        Spacer(Modifier.height(32.dp))
    }

    // ── Bottom-sheet dialogs (rendered when their flags are flipped) ──
    if (promoSheetOpen) {
        PromoCodeBottomSheet(
            initialCode = state.promoCode,
            onDismiss = { promoSheetOpen = false },
            onValidate = { code -> bookingVm.validatePromoCodeNow(code) },
            // VM persisted code + state; the sheet only signals so we can close it.
            onApplied = { _, _ -> },
        )
    }
}

/**
 * The two free-text notes the customer leaves for the cleaner: what to focus on,
 * and how to get in. Both are optional and reach the assigned cleaner read-only
 * on their order detail.
 */
@Composable
private fun InstructionsFields(
    specialInstructions: String,
    accessInstructions: String,
    onSpecialInstructionsChange: (String) -> Unit,
    onAccessInstructionsChange: (String) -> Unit,
) {
    Column(Modifier.fillMaxWidth()) {
        CleansiaTextField(
            value = specialInstructions,
            onValueChange = onSpecialInstructionsChange,
            label = stringResource(R.string.booking_special_instructions_hint),
            singleLine = false,
        )
        Spacer(Modifier.height(12.dp))
        CleansiaTextField(
            value = accessInstructions,
            onValueChange = onAccessInstructionsChange,
            label = stringResource(R.string.booking_access_instructions_hint),
            singleLine = false,
        )
    }
}

@Preview(widthDp = 390)
@Composable
private fun InstructionsFieldsPreview() {
    CleansiaTheme {
        InstructionsFields(
            specialInstructions = "",
            accessInstructions = "",
            onSpecialInstructionsChange = {},
            onAccessInstructionsChange = {},
        )
    }
}

@Preview(locale = "ru", widthDp = 320)
@Composable
private fun InstructionsFieldsRussianNarrowPreview() {
    CleansiaTheme {
        InstructionsFields(
            specialInstructions = "",
            accessInstructions = "",
            onSpecialInstructionsChange = {},
            onAccessInstructionsChange = {},
        )
    }
}

@Preview(locale = "uk", widthDp = 320)
@Composable
private fun InstructionsFieldsUkrainianNarrowPreview() {
    CleansiaTheme {
        InstructionsFields(
            specialInstructions = "",
            accessInstructions = "Бічна хвіртка, кодовий замок 4417.",
            onSpecialInstructionsChange = {},
            onAccessInstructionsChange = {},
        )
    }
}

@Preview(locale = "cs", widthDp = 320)
@Composable
private fun InstructionsFieldsCzechNarrowPreview() {
    CleansiaTheme {
        InstructionsFields(
            specialInstructions = "",
            accessInstructions = "",
            onSpecialInstructionsChange = {},
            onAccessInstructionsChange = {},
        )
    }
}

/* ── Cancellation policy card — 3-tier fee structure ── */

@Composable
private fun CancellationPolicyCard(
    membershipRepository: cz.cleansia.customer.core.memberships.MembershipRepository,
) {
    val membershipState by membershipRepository.current.collectAsState()
    // Backend BookingPolicy constants (mirror these exactly):
    //   StandardFreeWindowHours = 24  (free cancel ≥24h ahead)
    //   PenaltyWindowHours      = 4   (50% charge in 4–24h band; 100% under 4h)
    val standardFreeHours = 24
    val penaltyHours = 4
    // Plus may extend the free window. Only counts as a real perk when it's
    // strictly larger than the standard window — otherwise the badge would
    // be misleading.
    val rawPlusHours = membershipState
        ?.takeIf { it.hasMembership }
        ?.freeCancellationWindowHours
        ?.takeIf { it > 0 }
    val plusFreeHours = rawPlusHours?.takeIf { it > standardFreeHours }
    val freeHours = plusFreeHours ?: standardFreeHours
    // Mid-tier (50% charge) only renders when there's room between the free
    // window and the no-refund threshold. Plus members with a free window
    // wider than [penaltyHours] still see the mid-tier; if a future config
    // ever extends free below 4h the mid-tier vanishes (one-tier collapse).
    val showMidTier = freeHours > penaltyHours

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .padding(14.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                Icons.Outlined.Schedule,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.booking_cancel_title),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            if (plusFreeHours != null) {
                Spacer(Modifier.weight(1f))
                Text(
                    stringResource(R.string.booking_cancel_plus_badge),
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.primary,
                    modifier = Modifier
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f))
                        .padding(horizontal = 8.dp, vertical = 3.dp),
                )
            }
        }
        if (plusFreeHours != null) {
            Spacer(Modifier.height(2.dp))
            Text(
                text = stringResource(R.string.booking_cancel_plus_subtitle, plusFreeHours),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.primary,
            )
        }
        Spacer(Modifier.height(8.dp))
        PolicyTier(
            label = stringResource(R.string.booking_cancel_tier1_when_plus, freeHours),
            value = stringResource(R.string.booking_cancel_tier1_value),
            valueColor = SuccessText,
        )
        if (showMidTier) {
            PolicyTier(
                label = stringResource(
                    R.string.booking_cancel_tier2_when_range,
                    penaltyHours,
                    freeHours,
                ),
                value = stringResource(R.string.booking_cancel_tier2_value),
            )
        }
        PolicyTier(
            label = stringResource(R.string.booking_cancel_tier3_when_under, penaltyHours),
            value = stringResource(R.string.booking_cancel_tier3_value),
            valueColor = MaterialTheme.colorScheme.error,
        )
    }
}

@Composable
private fun PolicyTier(label: String, value: String, valueColor: androidx.compose.ui.graphics.Color = androidx.compose.ui.graphics.Color.Unspecified) {
    Row(
        Modifier.fillMaxWidth().padding(vertical = 3.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
        Text(
            value,
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
            color = if (valueColor == androidx.compose.ui.graphics.Color.Unspecified) MaterialTheme.colorScheme.onSurface else valueColor,
        )
    }
}

@Composable
private fun SummaryCard(content: @Composable () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .padding(14.dp),
    ) { content() }
}

/**
 * Add-on toggle list rendered just before the order summary. Each row is a
 * clickable card showing translated name + price; tapping flips the slug
 * in [BookingState.selectedExtraSlugs] which the parent watcher debounces
 * and re-quotes against the backend.
 */
@Composable
private fun ExtrasCard(
    extras: List<cz.cleansia.customer.core.catalog.ExtraListItem>,
    selectedSlugs: Set<String>,
    currencyCode: String?,
    onToggle: (String) -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .padding(14.dp),
    ) {
        Text(
            stringResource(R.string.booking_extras_header),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            stringResource(R.string.booking_extras_subtitle),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(10.dp))
        extras.forEach { extra ->
            val isSelected = extra.slug in selectedSlugs
            // Extras carry the same `translations` map as services and packages;
            // the card's own doc comment already promised a translated name.
            val extraName = localizedName(extra.translations, extra.name)
            val extraDescription = localizedDescription(extra.translations, extra.description)
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(12.dp))
                    .clickable { onToggle(extra.slug) }
                    .border(
                        width = if (isSelected) 2.dp else 1.dp,
                        color = if (isSelected) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.outlineVariant,
                        shape = RoundedCornerShape(12.dp),
                    )
                    .padding(horizontal = 12.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        extraName,
                        style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    if (!extraDescription.isNullOrBlank()) {
                        Spacer(Modifier.height(2.dp))
                        Text(
                            extraDescription,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                Spacer(Modifier.width(8.dp))
                Text(
                    formatOrderPrice(extra.price, currencyCode),
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = if (isSelected) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.onSurface,
                )
            }
            Spacer(Modifier.height(6.dp))
        }
    }
}

/**
 * Two-column row: small label on the left ("Address"), value on the right
 * ("Zenklova 545/6"). Easier to scan than a bare icon + text and matches the
 * receipt-style summary the customer expects on the confirm step.
 */
@Composable
private fun LabeledInfoRow(icon: ImageVector, label: String, value: String) {
    Row(Modifier.padding(vertical = 4.dp).fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, Modifier.size(16.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.width(8.dp))
        Text(
            label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.width(72.dp),
        )
        Text(
            value,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 2,
            overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun PaymentOption(icon: ImageVector, title: String, subtitle: String, selected: Boolean, onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(if (selected) selectionTint() else MaterialTheme.colorScheme.surface)
            .clickable(onClick = onClick),
    ) {
        if (selected) {
            Box(
                modifier = Modifier
                    .fillMaxHeight()
                    .width(3.dp)
                    .background(MaterialTheme.colorScheme.primary),
            )
        }
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .border(
                    if (selected) 0.dp else 1.dp,
                    if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                    RoundedCornerShape(14.dp),
                )
                .padding(start = if (selected) 17.dp else 14.dp, end = 14.dp, top = 14.dp, bottom = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Box(
                Modifier.size(40.dp).background(MaterialTheme.colorScheme.primary.copy(alpha = 0.15f), CircleShape),
                contentAlignment = Alignment.Center,
            ) { Icon(icon, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(20.dp)) }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(title, style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onSurface)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

@Composable
private fun TrustBadge(icon: ImageVector, text: String, modifier: Modifier) {
    Row(modifier = modifier, verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, tint = SuccessText, modifier = Modifier.size(20.dp))
        Spacer(Modifier.width(8.dp))
        Text(
            text,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 2,
            overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun SectionLabel(text: String) {
    Text(text, style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onBackground)
}

/**
 * Wolt-style entry row for the promo-code dialog. Tappable list row with a
 * leading ticket icon, title, and a trailing chevron when nothing's applied or
 * a "code · clear" pair when a validated code is in [appliedCode]. Tapping
 * the row anywhere fires [onClick].
 */
@Composable
private fun PromoCodeRow(
    appliedCode: String,
    onClick: () -> Unit,
    onClear: () -> Unit,
) {
    CodeEntryRow(
        icon = Icons.Outlined.ConfirmationNumber,
        title = stringResource(R.string.booking_promo_code_row_title),
        appliedCode = appliedCode,
        appliedSuffixRes = R.string.booking_promo_code_row_applied,
        clearContentDescriptionRes = R.string.booking_promo_code_row_clear,
        onClick = onClick,
        onClear = onClear,
    )
}


/**
 * Shared layout for the two row variants — leading icon, title (+ applied
 * subtitle), trailing chevron when empty / clear button when applied. Single
 * surface card matches other selectable rows on the screen (e.g. PaymentOption).
 */
@Composable
private fun CodeEntryRow(
    icon: ImageVector,
    title: String,
    appliedCode: String,
    appliedSuffixRes: Int,
    clearContentDescriptionRes: Int,
    onClick: () -> Unit,
    onClear: () -> Unit,
) {
    val hasApplied = appliedCode.isNotBlank()
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(36.dp)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.15f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(
                title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (hasApplied) {
                Text(
                    stringResource(appliedSuffixRes, appliedCode.trim().uppercase()),
                    style = MaterialTheme.typography.bodySmall,
                    color = SuccessText,
                )
            }
        }
        if (hasApplied) {
            IconButton(onClick = onClear) {
                Icon(
                    imageVector = Icons.Outlined.Close,
                    contentDescription = stringResource(clearContentDescriptionRes),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
        } else {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
        }
    }
}
