import CleansiaCore
import SwiftUI

struct OrderSectionCard<Content: View>: View {
    let title: String?
    let systemImage: String?
    @ViewBuilder let content: Content

    /// Titled card (header label + content).
    init(title: String, systemImage: String, @ViewBuilder content: () -> Content) {
        self.title = title
        self.systemImage = systemImage
        self.content = content()
    }

    /// Title-less card (bare surface, content only) — the Android `CustomerCard`
    /// has no section title (CustomerCard.kt:54-71).
    init(@ViewBuilder content: () -> Content) {
        title = nil
        systemImage = nil
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            if let title, let systemImage {
                Label(title, systemImage: systemImage)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

struct AccessCard: View {
    @Environment(\.locale) private var locale
    let instructions: String

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Label(L10n.Orders.accessSectionTitle, systemImage: "key.fill")
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.warningStar)
            Text(instructions)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.warningStar.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .id(locale.identifier)
    }
}

/// Shows the name and whichever location the server released to this caller — a street address,
/// or the coarse zone a browsing cleaner gets. Phone + SMS chips appear once a phone arrives.
///
/// Navigate is offered only against a street address: a maps app opened on a city name is worse
/// than no button.
struct CustomerCard: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail

    var body: some View {
        // Title-less, matching Android's bare CustomerCard (CustomerCard.kt:54-71);
        // only FromCustomerNotesCard carries the "From customer" section title.
        OrderSectionCard {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                if let name = order.customerName, !name.isEmpty {
                    Label(name, systemImage: "person")
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
                if let line = order.location.line {
                    Label(line, systemImage: "mappin.and.ellipse")
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                contactActions
            }
        }
        .id(locale.identifier)
    }

    @ViewBuilder
    private var contactActions: some View {
        let navigateTo = order.location.navigationTarget
        if order.showsCustomerContact || navigateTo != nil {
            HStack(spacing: Spacing.xs) {
                if order.showsCustomerContact {
                    ContactChip(
                        icon: "phone",
                        label: L10n.Orders.actionCall,
                        destination: ContactActions.callURL(phone: order.customerPhone)
                    )
                    ContactChip(
                        icon: "message",
                        label: L10n.Orders.actionSms,
                        destination: ContactActions.smsURL(phone: order.customerPhone)
                    )
                }
                if let navigateTo {
                    ContactChip(
                        icon: "map",
                        label: L10n.Orders.actionNavigate,
                        destination: ContactActions.mapsURL(
                            coordinate: navigateTo.coordinate,
                            address: navigateTo.line
                        )
                    )
                }
            }
            .padding(.top, Spacing.xxs)
        }
    }
}

private struct ContactChip: View {
    let icon: String
    let label: String
    let destination: URL?

    var body: some View {
        // `Link` rather than a Button calling `canOpenURL` + `openURL`: it needs
        // no `LSApplicationQueriesSchemes` declaration, so tel:/sms:/maps links
        // work with no Info.plist change. Same pattern as the customer app's
        // employee-call button (OrderDetailDetailsCards.swift).
        //
        // A nil destination means the field is unusable (a phone with no digits,
        // an address that is neither geocoded nor printable). Rendering nothing
        // is deliberate — a chip that looks tappable and is not is exactly the
        // bug this replaces.
        if let destination {
            Link(destination: destination) { chip }
        }
    }

    private var chip: some View {
        Label(label, systemImage: icon)
            .font(CleansiaTypography.labelLarge)
            .foregroundColor(CleansiaColors.primary)
            .padding(.horizontal, Spacing.s)
            .padding(.vertical, Spacing.xs)
            .frame(maxWidth: .infinity)
            .background(CleansiaColors.primary.opacity(0.1), in: Capsule())
    }
}

struct ScopeCard: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail

    private var roomsLine: String {
        var parts: [String] = []
        if order.rooms > 0 { parts.append(L10n.Orders.rooms(order.rooms)) }
        if order.bathrooms > 0 { parts.append(L10n.Orders.baths(order.bathrooms)) }
        return parts.isEmpty ? "—" : parts.joined(separator: " · ")
    }

    var body: some View {
        OrderSectionCard(title: L10n.Orders.scopeSectionTitle, systemImage: "house") {
            VStack(alignment: .leading, spacing: Spacing.s) {
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    Text(roomsLine)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurface)

                    if let crew = order.crew {
                        Text(OrdersFormat.crewLine(crew))
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }

                if !order.services.isEmpty || !order.packages.isEmpty {
                    sectionLabel(L10n.Orders.scopeServicesLabel)
                    ForEach(order.services, id: \.self) { service in
                        ScopeLine(label: service.localizedName(for: locale), value: nil)
                    }
                    ForEach(order.packages, id: \.self) { pkg in
                        ScopeLine(label: pkg.localizedName(for: locale), value: pkg.price.map {
                            OrdersFormat.money($0, symbol: order.currencySymbol)
                        })
                    }
                }

                if !order.extras.isEmpty {
                    sectionLabel(L10n.Orders.scopeExtrasLabel)
                    ForEach(order.extras, id: \.self) { slug in
                        HStack(spacing: Spacing.xs) {
                            Text(OrderExtras.emoji(slug))
                            Text(OrderExtras.name(slug))
                                .font(CleansiaTypography.bodyMedium)
                                .foregroundColor(CleansiaColors.onSurface)
                        }
                    }
                }
            }
        }
        .id(locale.identifier)
    }

    private func sectionLabel(_ text: String) -> some View {
        Text(text)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
    }
}

private struct ScopeLine: View {
    let label: String
    let value: String?

    var body: some View {
        HStack {
            Text(label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
            Spacer()
            if let value {
                Text(value)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
        }
    }
}

struct FromCustomerNotesCard: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail

    var body: some View {
        OrderSectionCard(title: L10n.Orders.fromCustomerSectionTitle, systemImage: "bubble.left") {
            VStack(alignment: .leading, spacing: Spacing.s) {
                noteBlock(L10n.Orders.noteGeneralLabel, order.customerNotes)
                noteBlock(L10n.Orders.noteSpecialLabel, order.specialInstructions)
            }
        }
        .id(locale.identifier)
    }

    @ViewBuilder
    private func noteBlock(_ label: String, _ body: String?) -> some View {
        if let body, !body.trimmingCharacters(in: .whitespaces).isEmpty {
            VStack(alignment: .leading, spacing: 2) {
                Text(label)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Text(body)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
        }
    }
}

struct PaymentCard: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail

    private var payment: OrderDetailPayment {
        order.payment
    }

    var body: some View {
        OrderSectionCard(title: L10n.Orders.paymentSectionTitle, systemImage: "creditcard") {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                if payment.hasBreakdown, let subtotal = payment.subtotal {
                    paymentRow(L10n.Orders.paymentSubtotal, subtotal, emphasis: false)
                    discountRow(L10n.Orders.paymentTierDiscount, payment.tierDiscount)
                    discountRow(L10n.Orders.paymentMembershipDiscount, payment.membershipDiscount)
                    discountRow(L10n.Orders.paymentPromoDiscount, payment.promoDiscount)
                    Divider()
                }
                if let total = payment.total {
                    paymentRow(L10n.Orders.paymentTotal, total, emphasis: true)
                }
                HStack {
                    Text(L10n.Orders.paymentMethod(PaymentPresentation.methodLabel(payment.typeCode)))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Spacer()
                    PaymentStatusPill(statusCode: payment.statusCode)
                }
            }
        }
        .id(locale.identifier)
    }

    private func paymentRow(_ label: String, _ amount: Double, emphasis: Bool) -> some View {
        HStack {
            Text(label)
                .font(emphasis ? CleansiaTypography.titleMedium : CleansiaTypography.bodyMedium)
                .foregroundColor(emphasis ? CleansiaColors.onSurface : CleansiaColors.onSurfaceVariant)
            Spacer()
            Text(OrdersFormat.money(amount, symbol: order.currencySymbol))
                .font(emphasis ? CleansiaTypography.titleMedium : CleansiaTypography.bodyMedium)
                .foregroundColor(emphasis ? CleansiaColors.primary : CleansiaColors.onSurface)
        }
    }

    @ViewBuilder
    private func discountRow(_ label: String, _ amount: Double?) -> some View {
        if let amount, amount > 0 {
            HStack {
                Text(label)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Spacer()
                Text("-\(OrdersFormat.money(amount, symbol: order.currencySymbol))")
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.primary)
            }
        }
    }
}

/// Colour-coded payment-status pill: green settled, amber pending, red
/// failed/disputed, neutral for refunds and for a status we don't map yet (the
/// `PaymentStatusPill` parity). Label and tint both come from `PaymentPresentation`.
private struct PaymentStatusPill: View {
    let statusCode: Int?

    private var tint: Color {
        switch PaymentPresentation.statusSeverity(statusCode) {
        case .success: CleansiaColors.successText
        case .warning: CleansiaColors.warningStar
        case .error: CleansiaColors.error
        case .neutral: CleansiaColors.onSurfaceVariant
        }
    }

    private var label: String {
        PaymentPresentation.statusLabel(statusCode)
    }

    var body: some View {
        Text(label)
            .font(CleansiaTypography.labelMedium)
            .foregroundColor(tint)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, 2)
            .background(tint.opacity(0.12), in: Capsule())
    }
}
