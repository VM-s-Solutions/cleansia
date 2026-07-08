import CleansiaCore
import SwiftUI

struct SubscribePlusScreen: View {
    @StateObject private var vm: MembershipViewModel
    @Environment(\.snackbarController) private var snackbar
    private let paymentSheet: PaymentSheetPresenting
    private let onBack: () -> Void
    private let onSubscribed: () -> Void

    @State private var selectedPlanCode = ""
    @State private var navigatedAway = false

    init(
        repository: MembershipRepository,
        snackbar: SnackbarController,
        paymentSheet: PaymentSheetPresenting,
        onBack: @escaping () -> Void,
        onSubscribed: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: MembershipViewModel(repository: repository, snackbar: snackbar))
        self.paymentSheet = paymentSheet
        self.onBack = onBack
        self.onSubscribed = onSubscribed
    }

    private var selectedPlan: MembershipPlan? {
        vm.plans.first { $0.code == selectedPlanCode }
    }

    var body: some View {
        ZStack(alignment: .bottom) {
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    HeroBlock(
                        plans: vm.plans,
                        selectedPlanCode: selectedPlanCode,
                        selectedPlan: selectedPlan,
                        onSelectPlan: { selectedPlanCode = $0 },
                        onBack: onBack
                    )
                    SocialProofTile()
                    PerksSection()
                    Color.clear.frame(height: 140)
                }
            }
            if vm.canSubscribe {
                StickyCtaBar(
                    label: (selectedPlan?.trialPeriodDays ?? 0) > 0
                        ? L10n.Membership.ctaStartTrial : L10n.Membership.ctaSubscribe,
                    disclosure: disclosure,
                    enabled: !vm.submitState.isSubmitting && !selectedPlanCode.isEmpty,
                    onTap: subscribe
                )
            }
            BusyMascotOverlay(
                visible: vm.submitState.isSubmitting,
                message: L10n.Membership.busySubscribePlus
            )
        }
        .background(CleansiaColors.background.ignoresSafeArea())
        .navigationBarBackButtonHidden(true)
        .toolbar(.hidden, for: .navigationBar)
        .task {
            await vm.load()
            if selectedPlanCode.isEmpty {
                selectedPlanCode = vm.plans.first { $0.billingInterval == 1 }?.code
                    ?? vm.plans.first?.code ?? ""
            }
        }
        .onChange(of: vm.current?.hasMembership) { hasMembership in
            if hasMembership == true, !navigatedAway {
                navigatedAway = true
                onBack()
            }
        }
    }

    private var disclosure: String {
        guard let plan = selectedPlan, plan.trialPeriodDays > 0 else { return L10n.Membership.disclosure }
        let price = MembershipFormat.price(plan.price)
        return plan.isAnnual
            ? L10n.Membership.ctaDisclosureTrialYear(price)
            : L10n.Membership.ctaDisclosureTrial(price)
    }

    private func subscribe() {
        guard !selectedPlanCode.isEmpty else { return }
        Task {
            switch await vm.startSubscribe(planCode: selectedPlanCode) {
            case let .needsPaymentMethod(presentation):
                await presentPaymentSheet(presentation)
            case .alreadyActive:
                snackbar.showSuccess(L10n.Membership.alreadyActive)
                onBack()
            case .subscribed, .failed:
                break
            }
        }
    }

    private func presentPaymentSheet(_ presentation: PaymentSheetPresentation) async {
        let outcome = await paymentSheet.present(presentation)
        switch outcome {
        case .completed:
            if case let .subscribed = await vm.confirmSubscribe(planCode: selectedPlanCode), !navigatedAway {
                navigatedAway = true
                onSubscribed()
            }
        case .canceled:
            snackbar.showError(L10n.localized("error_payment_cancelled"))
        case .failed:
            snackbar.showError(L10n.localized("error_payment_failed"))
        }
    }
}

private struct HeroBlock: View {
    let plans: [MembershipPlan]
    let selectedPlanCode: String
    let selectedPlan: MembershipPlan?
    let onSelectPlan: (String) -> Void
    let onBack: () -> Void

    var body: some View {
        ZStack(alignment: .bottomTrailing) {
            VStack(alignment: .leading, spacing: Spacing.m) {
                HStack {
                    Button(action: onBack) {
                        Image(systemName: "arrow.left")
                            .font(.system(size: 18, weight: .semibold))
                            .foregroundColor(.white)
                    }
                    Spacer()
                }
                HStack(spacing: Spacing.xs) {
                    Spacer()
                    Text(verbatim: "Cleansia")
                        .font(CleansiaTypography.displayMedium)
                        .foregroundColor(.white)
                    Text(L10n.Membership.inactiveBadge)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(MembershipPalette.slate900)
                        .padding(.horizontal, Spacing.s)
                        .padding(.vertical, 4)
                        .background(MembershipPalette.sky400, in: RoundedRectangle(cornerRadius: 10))
                    Spacer()
                }
                Text(L10n.Membership.heroHeadline)
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity, alignment: .center)
                    .multilineTextAlignment(.center)
                priceBlock
                if plans.count >= 2 {
                    PlanSwitcher(plans: plans, selectedCode: selectedPlanCode, onSelect: onSelectPlan)
                }
                Spacer().frame(height: 56)
            }
            .padding(Spacing.ml)
            Mascot.waving.image
                .resizable()
                .scaledToFit()
                .frame(width: 96, height: 96)
                .padding(.trailing, Spacing.s)
                .padding(.bottom, Spacing.xxs)
        }
        .frame(maxWidth: .infinity)
        .background(
            LinearGradient(
                colors: [MembershipPalette.sky950, MembershipPalette.slate900],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea(edges: .top)
        )
    }

    @ViewBuilder
    private var priceBlock: some View {
        let trialDays = selectedPlan?.trialPeriodDays ?? 0
        let regularPrice = MembershipFormat.price(selectedPlan?.price ?? 0)
        let isAnnual = selectedPlan?.isAnnual ?? false
        if trialDays > 0 {
            VStack(spacing: Spacing.xs) {
                Text(L10n.Membership.heroTrialPrice(trialDays))
                    .font(CleansiaTypography.headlineLarge)
                    .foregroundColor(.white)
                    .lineLimit(1)
                Text(isAnnual
                    ? L10n.Membership.heroThenPriceYear(regularPrice)
                    : L10n.Membership.heroThenPrice(regularPrice))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(.white.opacity(0.7))
                    .strikethrough()
            }
            .frame(maxWidth: .infinity)
        } else {
            Text(isAnnual ? L10n.Membership.planPerYear(regularPrice) : L10n.Membership.planPerMonth(regularPrice))
                .font(CleansiaTypography.headlineLarge)
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
        }
    }
}

private struct PlanSwitcher: View {
    let plans: [MembershipPlan]
    let selectedCode: String
    let onSelect: (String) -> Void
    @Namespace private var thumb

    var body: some View {
        HStack(spacing: 0) {
            ForEach(plans) { plan in
                segment(plan)
            }
        }
        .padding(3)
        .background(Color.white.opacity(0.10), in: Capsule())
        .frame(maxWidth: .infinity)
        .animation(.spring(response: 0.32, dampingFraction: 0.72), value: selectedCode)
    }

    private func segment(_ plan: MembershipPlan) -> some View {
        let selected = plan.code == selectedCode
        return Button { onSelect(plan.code) } label: {
            HStack(spacing: Spacing.xxs) {
                Text(plan.isAnnual ? L10n.Membership.planAnnual : L10n.Membership.planMonthly)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(selected ? MembershipPalette.slate900 : .white)
                if plan.savingsPercentVsMonthly > 0 {
                    Text(verbatim: "−\(Int(plan.savingsPercentVsMonthly))%")
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(selected ? MembershipPalette.slate900 : MembershipPalette.sky400)
                }
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, Spacing.s)
            .background {
                if selected {
                    Capsule()
                        .fill(MembershipPalette.sky400)
                        .matchedGeometryEffect(id: "thumb", in: thumb)
                }
            }
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
    }
}

private struct SocialProofTile: View {
    var body: some View {
        HStack(spacing: Spacing.m) {
            Image(systemName: "chart.line.uptrend.xyaxis")
                .font(.system(size: 20))
                .foregroundColor(CleansiaColors.primary)
                .frame(width: 40, height: 40)
                .background(MembershipPalette.sky400.opacity(0.2), in: Circle())
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Membership.socialProofHeadline)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.primary)
                Text(L10n.Membership.socialProofSub)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
        }
        .padding(Spacing.m)
        .background(MembershipPalette.sky400.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .padding(.horizontal, Spacing.ml)
    }
}

private struct PerksSection: View {
    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Membership.perksSectionTitle)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
            PerkTile(icon: "tag", title: L10n.Membership.perkDiscountTitle, desc: L10n.Membership.perkDiscountDesc)
            PerkTile(
                icon: "checkmark.circle",
                title: L10n.Membership.perkCancellationTitle,
                desc: L10n.Membership.perkCancellationDesc
            )
            PerkTile(
                icon: "person",
                title: L10n.Membership.perkFavoriteCleanerTitle,
                desc: L10n.Membership.perkFavoriteCleanerDesc
            )
            PerkTile(icon: "repeat", title: L10n.Membership.perkRecurringTitle, desc: L10n.Membership.perkRecurringDesc)
            PerkTile(icon: "bolt", title: L10n.Membership.perkExpressTitle, desc: L10n.Membership.perkExpressDesc)
        }
        .padding(.horizontal, Spacing.ml)
    }
}

private struct PerkTile: View {
    let icon: String
    let title: String
    let desc: String

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.m) {
            Image(systemName: icon)
                .font(.system(size: 22))
                .foregroundColor(CleansiaColors.primary)
                .frame(width: 44, height: 44)
                .background(CleansiaColors.primary.opacity(0.12), in: Circle())
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(title)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(desc)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private struct StickyCtaBar: View {
    let label: String
    let disclosure: String
    let enabled: Bool
    let onTap: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            CleansiaPrimaryButton(label, leadingIcon: "crown", enabled: enabled, action: onTap)
            Text(disclosure)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
        }
        .padding(Spacing.ml)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
    }
}

enum MembershipPalette {
    static let sky400 = Color(red: 0.22, green: 0.65, blue: 0.94)
    static let sky950 = Color(red: 0.03, green: 0.16, blue: 0.30)
    static let slate900 = Color(red: 0.06, green: 0.09, blue: 0.16)
    static let premiumGold = Color(red: 0.85, green: 0.47, blue: 0.02)
    static let endingAccent = Color(red: 0.73, green: 0.11, blue: 0.11)
}

enum MembershipFormat {
    static func price(_ amount: Double) -> String {
        let rounded = amount.truncatingRemainder(dividingBy: 1) == 0
            ? String(Int(amount))
            : String(format: "%.2f", amount)
        return "\(rounded) Kč"
    }

    static func periodEnd(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .none
        return formatter.string(from: date)
    }
}
