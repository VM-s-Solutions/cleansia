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
        GeometryReader { proxy in
            ZStack(alignment: .bottom) {
                ScrollView {
                    VStack(alignment: .leading, spacing: Spacing.l) {
                        HeroBlock(
                            plans: vm.plans,
                            selectedPlanCode: selectedPlanCode,
                            selectedPlan: selectedPlan,
                            topInset: proxy.safeAreaInsets.top,
                            onSelectPlan: { selectedPlanCode = $0 },
                            onBack: onBack
                        )
                        SocialProofTile()
                        PerksSection(showExpress: selectedPlan?.allowsExpressUpgrade == true)
                        Color.clear.frame(height: 140)
                    }
                }
                .ignoresSafeArea(.container, edges: .top)
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
        }
        // Mounted here as well as at the shell root: the root's SwiftUI update pass runs BEFORE this
        // screen's appearance transition, and the bar is hidden inside that transition. Safe to mount
        // per-screen because the delegate is static — see `InteractivePopGestureEnabler`.
        .background(InteractivePopGestureEnabler())
        // No `.navigationBarBackButtonHidden` here. The bar is hidden anyway so nothing shows either
        // way, but setting it also sets `hidesBackButton`, which is a second thing UIKit's own
        // interactive-pop delegate refuses on. Swipe-back itself is owned by
        // `InteractivePopGestureEnabler` at the shell root, which replaces that delegate — see the note
        // there for why the earlier `isEnabled`-only version could never have worked on this screen.
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
            if case .subscribed = await vm.confirmSubscribe(planCode: selectedPlanCode), !navigatedAway {
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

/// The character perched on the plan switcher: his size, and how much of him sits below the control's
/// top edge so he reads as resting on it rather than hovering.
private let mascotSize: CGFloat = 84
private let mascotPerch: CGFloat = 14

private struct HeroBlock: View {
    let plans: [MembershipPlan]
    let selectedPlanCode: String
    let selectedPlan: MembershipPlan?
    var topInset: CGFloat = 0
    let onSelectPlan: (String) -> Void
    let onBack: () -> Void

    var body: some View {
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
                    .cleansiaFont(CleansiaTypography.displayMedium)
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
                .cleansiaFont(CleansiaTypography.headlineMedium)
                .foregroundColor(.white)
                .frame(maxWidth: .infinity, alignment: .center)
                .multilineTextAlignment(.center)
            priceBlock
            if plans.count >= 2 {
                PlanSwitcher(plans: plans, selectedCode: selectedPlanCode, onSelect: onSelectPlan)
                    // ORDER MATTERS: the overlay goes on FIRST, so it anchors to the control itself.
                    // With the padding applied before it, the overlay measured the PADDED frame and the
                    // character rose by exactly that padding while the switcher went down — the two
                    // moved apart instead of together.
                    // He sits ON the Annual segment: anchored to the switcher's top-trailing corner
                    // and lifted by his own height less an overlap, so his feet rest on the control's
                    // top edge rather than floating above it. Non-interactive, or he would eat the
                    // taps meant for the segment he is sitting on.
                    .overlay(alignment: .topTrailing) {
                        Mascot.waving.image
                            .resizable()
                            .scaledToFit()
                            .frame(width: mascotSize, height: mascotSize)
                            .offset(x: -Spacing.m, y: -mascotSize + mascotPerch)
                            .allowsHitTesting(false)
                            .accessibilityHidden(true)
                    }
                    // Applied last, so it moves the control AND the character riding it as one.
                    .padding(.top, Spacing.l)
            }
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.bottom, Spacing.ml)
        .padding(.top, Spacing.ml + topInset)
        .frame(maxWidth: .infinity)
        .background(
            LinearGradient(
                colors: [MembershipPalette.sky950, MembershipPalette.slate900],
                startPoint: .top,
                endPoint: .bottom
            )
        )
        // The GeometryReader safe-area inset settles 0 → real on first layout; an
        // ambient transaction would animate that top-padding change into a visible
        // slide. Pin it so the header paints in its final position (round-6 fix
        // intact; iOS fix-round 8).
        .animation(nil, value: topInset)
    }

    @ViewBuilder
    private var priceBlock: some View {
        let trialDays = selectedPlan?.trialPeriodDays ?? 0
        let regularPrice = MembershipFormat.price(selectedPlan?.price ?? 0)
        let isAnnual = selectedPlan?.isAnnual ?? false
        if trialDays > 0 {
            VStack(spacing: Spacing.xs) {
                Text(L10n.Membership.heroTrialPrice(trialDays))
                    .cleansiaFont(CleansiaTypography.headlineLarge)
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
                .cleansiaFont(CleansiaTypography.headlineLarge)
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
        }
    }
}

private struct PlanSwitcher: View {
    let plans: [MembershipPlan]
    let selectedCode: String
    let onSelect: (String) -> Void

    private static let height: CGFloat = 44

    private var selectedIndex: Int {
        plans.firstIndex { $0.code == selectedCode } ?? 0
    }

    var body: some View {
        GeometryReader { geo in
            let segmentWidth = geo.size.width / CGFloat(max(plans.count, 1))
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(MembershipPalette.sky400)
                    .frame(width: segmentWidth)
                    .offset(x: segmentWidth * CGFloat(selectedIndex))
                    .animation(.spring(response: 0.28, dampingFraction: 0.86), value: selectedIndex)
                HStack(spacing: 0) {
                    ForEach(plans) { plan in
                        segment(plan).frame(width: segmentWidth)
                    }
                }
            }
        }
        .frame(height: Self.height)
        .padding(3)
        .background(Color.white.opacity(0.10), in: Capsule())
        .frame(maxWidth: .infinity)
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
            .frame(maxWidth: .infinity, maxHeight: .infinity)
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
    let showExpress: Bool

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
            if showExpress {
                PerkTile(
                    icon: "bolt",
                    title: L10n.Membership.perkExpressTitle,
                    desc: L10n.Membership.perkExpressDesc
                )
            }
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
