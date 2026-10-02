import CleansiaCore
import SwiftUI

struct ConfirmStep: View {
    @ObservedObject var viewModel: BookingViewModel
    @StateObject private var extras = PreferredCleanerViewModel()

    @State private var showPromoSheet = false

    private var quote: BookingQuote? {
        viewModel.quoteState.quote
    }

    private var tierDiscount: Double {
        quote?.tierDiscountAmount ?? 0
    }

    private var membershipDiscount: Double {
        quote?.membershipDiscountAmount ?? 0
    }

    private var promoDiscount: Double {
        viewModel.promoState.discount
    }

    private var combinedServerDiscount: Double {
        tierDiscount + membershipDiscount
    }

    private var priceSummary: BookingPriceSummary {
        BookingPriceSummary.resolve(quote: quote, discount: viewModel.effectiveDiscount)
    }

    private var currencyCode: String {
        viewModel.displayCurrencyCode ?? ""
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                extrasCard
                summaryCard
                promoRow
                paymentSection
                specialInstructionsSection
                accessInstructionsSection
                PreferredCleanerPicker(
                    cleaners: extras.isVisible ? extras.cleaners : [],
                    selectedId: viewModel.state.preferredEmployeeId,
                    onSelect: setPreferredCleaner
                )
                CancellationPolicyCard(policy: extras.cancellationPolicy)
                termsRow
                earlyPerformanceRow
                ContractNotice()
                TrustBadges(insurance: viewModel.insurance)
            }
            .padding(Spacing.l)
        }
        .task { await viewModel.loadExtras() }
        .task { await extras.load(membership: viewModel.loadMembership()) }
        .sheet(isPresented: $showPromoSheet) {
            PromoCodeSheet(
                initialCode: viewModel.state.promoCode,
                currencyCode: currencyCode,
                onValidate: { code in await viewModel.validatePromoCode(code) },
                onDismiss: { showPromoSheet = false }
            )
        }
    }

    @ViewBuilder
    private var extrasCard: some View {
        if let extras = viewModel.extrasState.loadedValue, !extras.isEmpty {
            ExtrasCard(
                extras: extras,
                selectedSlugs: viewModel.state.selectedExtraSlugs,
                currencyCode: currencyCode,
                onToggle: { viewModel.toggleExtra($0) }
            )
        }
    }

    private var summaryCard: some View {
        SummaryCard(
            state: viewModel.state,
            summary: priceSummary,
            promoDiscount: promoDiscount,
            membershipDiscount: membershipDiscount,
            tierDiscount: tierDiscount,
            combinedServerDiscount: combinedServerDiscount,
            unmetTierDiscountFloor: viewModel.unmetTierDiscountFloor,
            currencyCode: currencyCode
        )
    }

    private var promoRow: some View {
        CodeEntryRow(
            systemImage: "ticket",
            title: L10n.Booking.promoRowTitle,
            appliedCode: appliedPromoCode,
            clearLabel: L10n.Booking.promoRowClear,
            appliedText: L10n.Booking.promoRowApplied,
            onTap: { showPromoSheet = true },
            onClear: { viewModel.clearPromoCode() }
        )
    }

    private var appliedPromoCode: String {
        if case .valid = viewModel.promoState { return viewModel.state.promoCode }
        return ""
    }

    private var paymentSection: some View {
        let cash = viewModel.cashEligibility
        return VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Booking.paymentMethod)
                .font(CleansiaTypography.titleMedium)
                .fontWeight(.semibold)
                .foregroundColor(CleansiaColors.onBackground)
            if viewModel.isCardPaymentAvailable {
                PaymentOption(
                    systemImage: "creditcard",
                    title: L10n.Booking.payCard,
                    subtitle: L10n.Booking.payCardDesc,
                    selected: viewModel.state.paymentMethod == .card,
                    action: { viewModel.selectPayment(.card) }
                )
            }
            if viewModel.offersCardSaving {
                SaveCardOption(saved: Binding(
                    get: { viewModel.state.saveCard },
                    set: viewModel.setSaveCard
                ))
            }
            PaymentOption(
                systemImage: "banknote",
                title: L10n.Booking.payCash,
                subtitle: L10n.Booking.payCashDesc,
                selected: viewModel.state.paymentMethod == .cash,
                enabled: cash == .available,
                action: { viewModel.selectPayment(.cash) }
            )
            if viewModel.cashCleared, cash != .available {
                PaymentNote(systemImage: "exclamationmark.circle", text: L10n.Booking.cashCleared, warns: true)
            }
            if let reason = L10n.Booking.cashReason(cash) {
                PaymentNote(systemImage: "info.circle", text: reason)
            }
            if viewModel.needsCardGuarantee {
                CardGuaranteeConsent(accepted: Binding(
                    get: { viewModel.state.cardGuaranteeAccepted },
                    set: viewModel.setCardGuaranteeAccepted
                ))
            }
        }
    }

    private var specialInstructionsSection: some View {
        InstructionsField(
            hint: L10n.Booking.specialInstructionsHint,
            text: Binding(
                get: { viewModel.state.specialInstructions },
                set: viewModel.setSpecialInstructions
            )
        )
    }

    private var accessInstructionsSection: some View {
        InstructionsField(
            hint: L10n.Booking.accessInstructionsHint,
            text: Binding(
                get: { viewModel.state.accessInstructions },
                set: viewModel.setAccessInstructions
            )
        )
    }

    private func setPreferredCleaner(_ id: String?) {
        viewModel.update { current in
            var next = current
            next.preferredEmployeeId = id
            return next
        }
    }

    /// The same two documents the sign-up tick names, asked only of an account that has not already
    /// granted both. Gates the slide-to-confirm and rides `termsAccepted` on CreateOrder.
    @ViewBuilder
    private var termsRow: some View {
        if !viewModel.alreadyConsented {
            CleansiaConsentCheckbox(
                checked: Binding(
                    get: { viewModel.state.termsAccepted },
                    set: setTermsAccepted
                ),
                markdown: L10n.Auth.acceptTerms,
                toggleAccessibilityLabel: L10n.Auth.acceptTermsToggle
            )
        }
    }

    private func setTermsAccepted(_ accepted: Bool) {
        viewModel.update { current in
            var next = current
            next.termsAccepted = accepted
            return next
        }
    }

    /// Asked on every booking, whatever the account already consented to: the request belongs to this
    /// contract, not to the account. Gates the slide-to-confirm and rides `earlyPerformanceRequested`.
    private var earlyPerformanceRow: some View {
        CleansiaConsentCheckbox(
            checked: Binding(
                get: { viewModel.state.earlyPerformanceRequested },
                set: setEarlyPerformanceRequested
            ),
            markdown: L10n.Booking.earlyPerformanceRequest,
            toggleAccessibilityLabel: L10n.Booking.earlyPerformanceRequestToggle
        )
    }

    private func setEarlyPerformanceRequested(_ requested: Bool) {
        viewModel.update { current in
            var next = current
            next.earlyPerformanceRequested = requested
            return next
        }
    }
}

/// Cash is guaranteed by a saved card; the first cash booking asks for the consent before PaymentSheet
/// saves one.
private struct CardGuaranteeConsent: View {
    @Binding var accepted: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Booking.cardGuaranteeTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            Text(L10n.Booking.cardGuaranteeBody)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
            CleansiaConsentCheckbox(
                checked: $accepted,
                markdown: L10n.Booking.cardGuaranteeConsent,
                toggleAccessibilityLabel: L10n.Booking.cardGuaranteeTitle
            )
        }
        .padding(Spacing.s)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

/// The contract the confirmation concludes with the operating company, named at the offer whether or not
/// the account already consented: an information line with the terms behind it, never a tick.
private struct ContractNotice: View {
    var body: some View {
        Text(ConsentMarkdown.styled(L10n.Booking.contractNotice))
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .tint(CleansiaColors.primary)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

#if DEBUG
    struct ConfirmStep_Previews: PreviewProvider {
        static var previews: some View {
            ConfirmStep(viewModel: BookingViewModel())
                .background(CleansiaColors.background)
        }
    }
#endif
