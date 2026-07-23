import CleansiaCore
import SwiftUI

struct ConfirmStep: View {
    @ObservedObject var viewModel: BookingViewModel
    @StateObject private var extras = PreferredCleanerViewModel()

    @State private var showPromoSheet = false

    private var quote: BookingQuote? {
        viewModel.quoteState.quote
    }

    private var basePrice: Double {
        quote?.totalPrice ?? 0
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

    private var effectiveDiscount: Double {
        max(combinedServerDiscount, promoDiscount)
    }

    private var isExpress: Bool {
        BookingPricing.requiresExpressSurcharge(cleaningAt: viewModel.state.selectedInstant)
    }

    private var discountedSubtotal: Double {
        max(basePrice - effectiveDiscount, 0)
    }

    private var surcharge: Double {
        BookingPricing.expressSurchargeAmount(
            basePrice: discountedSubtotal,
            cleaningAt: viewModel.state.selectedInstant
        )
    }

    private var finalTotal: Double {
        BookingPricing.finalTotal(
            basePrice: basePrice,
            cleaningAt: viewModel.state.selectedInstant,
            tierDiscount: combinedServerDiscount,
            promoDiscount: promoDiscount
        )
    }

    private var currencyCode: String {
        quote?.currencyCode ?? "CZK"
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                extrasCard
                summaryCard
                promoRow
                paymentSection
                specialInstructionsSection
                PreferredCleanerPicker(
                    viewModel: extras,
                    selectedId: viewModel.state.preferredEmployeeId,
                    onSelect: setPreferredCleaner
                )
                CancellationPolicyCard(policy: extras.cancellationPolicy)
                TrustBadges()
            }
            .padding(Spacing.l)
        }
        .task { await viewModel.loadExtras() }
        .task { await extras.load() }
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
            basePrice: basePrice,
            promoDiscount: promoDiscount,
            membershipDiscount: membershipDiscount,
            tierDiscount: tierDiscount,
            combinedServerDiscount: combinedServerDiscount,
            effectiveDiscount: effectiveDiscount,
            isExpress: isExpress,
            surcharge: surcharge,
            finalTotal: finalTotal,
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
        VStack(alignment: .leading, spacing: Spacing.s) {
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
                    action: { setPayment(.card) }
                )
            }
            PaymentOption(
                systemImage: "banknote",
                title: L10n.Booking.payCash,
                subtitle: L10n.Booking.payCashDesc,
                selected: viewModel.state.paymentMethod == .cash,
                action: { setPayment(.cash) }
            )
        }
    }

    private func setPayment(_ method: PaymentMethod) {
        viewModel.update { current in
            var next = current
            next.paymentMethod = method
            return next
        }
    }

    private var specialInstructionsSection: some View {
        SpecialInstructionsField(text: Binding(
            get: { viewModel.state.specialInstructions },
            set: { value in
                viewModel.update { current in
                    var next = current
                    next.specialInstructions = value
                    return next
                }
            }
        ))
    }

    private func setPreferredCleaner(_ id: String?) {
        viewModel.update { current in
            var next = current
            next.preferredEmployeeId = id
            return next
        }
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
