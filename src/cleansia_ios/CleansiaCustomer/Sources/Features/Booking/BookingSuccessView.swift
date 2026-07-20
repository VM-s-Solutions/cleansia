import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct BookingSuccessView: View {
    let confirmationCode: String
    let onViewOrder: (() -> Void)?
    let onDone: () -> Void

    @StateObject private var orderVM: BookingSuccessViewModel
    @Environment(\.locale) private var locale

    init(
        confirmationCode: String,
        orderId: String,
        loadOrder: @escaping @Sendable (String) async -> OrderItem?,
        warmOrders: @escaping @Sendable () async -> Void = {},
        onViewOrder: (() -> Void)?,
        onDone: @escaping () -> Void
    ) {
        self.confirmationCode = confirmationCode
        self.onViewOrder = onViewOrder
        self.onDone = onDone
        _orderVM = StateObject(wrappedValue: BookingSuccessViewModel(
            orderId: orderId,
            fetch: loadOrder,
            warmOrders: warmOrders
        ))
    }

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                AnimatedMascotView(.welcoming, loop: false, fallback: .waving)
                    .frame(width: 220, height: 220)
                VStack(spacing: Spacing.xs) {
                    Text(L10n.Booking.successTitle)
                        .font(CleansiaTypography.headlineSmall)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)
                    Text(L10n.Booking.successSubtitle)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)
                }
                let code = orderVM.effectiveCode(fallback: confirmationCode)
                if !code.isBlank {
                    confirmationCard(code)
                }
                enrichment
                timelineCard
                Text(L10n.Booking.successWhatsNext)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                VStack(spacing: Spacing.s) {
                    if let onViewOrder {
                        CleansiaPrimaryButton(L10n.Orders.viewOrder, action: onViewOrder)
                        CleansiaOutlinedButton(L10n.Booking.successGoHome, action: onDone)
                    } else {
                        CleansiaPrimaryButton(L10n.Booking.successGoHome, action: onDone)
                    }
                }
            }
            .padding(Spacing.l)
        }
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await orderVM.load() }
    }

    private func confirmationCard(_ code: String) -> some View {
        VStack(spacing: Spacing.xxs) {
            Text(L10n.Booking.successConfirmationCode)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(code)
                .font(CleansiaTypography.titleLarge)
                .fontWeight(.bold)
                .foregroundColor(CleansiaColors.primary)
                .textSelection(.enabled)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity)
        .successCard()
    }

    /// The Android enrichment block: a small spinner while the order loads, the
    /// summary card once it lands, and nothing on failure — the code pill and
    /// timeline still render.
    @ViewBuilder
    private var enrichment: some View {
        switch orderVM.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
        case let .loaded(order):
            let rows = summaryRows(order)
            if !rows.isEmpty {
                summaryCard(rows)
            }
        case .error:
            EmptyView()
        }
    }

    private func summaryRows(_ order: OrderItem) -> [SummaryRow] {
        var rows: [SummaryRow] = []
        if order.cleaningDateTime != nil {
            rows.append(SummaryRow(
                label: L10n.Booking.successArrivalLabel,
                value: OrdersFormat.dateRange(
                    order.cleaningDateTime,
                    estimatedMinutes: order.estimatedTime ?? 0,
                    locale: locale
                )
            ))
        }
        if let address = addressLine(order.address) {
            rows.append(SummaryRow(label: L10n.Booking.successAddressLabel, value: address))
        }
        if let total = order.totalPrice, total > 0 {
            rows.append(SummaryRow(
                label: L10n.Booking.successTotalLabel,
                value: OrdersFormat.price(total, currencyCode: order.currency?.code)
            ))
        }
        return rows
    }

    private func summaryCard(_ rows: [SummaryRow]) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(rows) { row in
                HStack(alignment: .top, spacing: Spacing.s) {
                    Text(row.label)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Spacer()
                    Text(row.value)
                        .font(CleansiaTypography.bodyMedium)
                        .fontWeight(.semibold)
                        .foregroundColor(CleansiaColors.onSurface)
                        .multilineTextAlignment(.trailing)
                }
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .successCard()
    }

    private func addressLine(_ address: OrderAddress?) -> String? {
        guard let address else { return nil }
        let parts = [address.street, address.city]
            .compactMap { $0 }
            .filter { !$0.isBlank }
        return parts.isEmpty ? nil : parts.joined(separator: ", ")
    }

    private var timelineCard: some View {
        let entries = BookingSuccessTimeline.entries(
            status: orderVM.order?.status,
            cleanerAssigned: !(orderVM.order?.assignedEmployees ?? []).isEmpty
        )
        return VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Booking.successProgress)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
            VStack(alignment: .leading, spacing: 0) {
                ForEach(entries, id: \.step) { entry in
                    timelineRow(entry, isLast: entry.step == entries.last?.step)
                }
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .successCard()
    }

    private func timelineRow(_ entry: BookingSuccessTimelineEntry, isLast: Bool) -> some View {
        let dotColor = entry.state == .pending ? CleansiaColors.outlineVariant : CleansiaColors.primary
        let connectorColor = entry.state == .done ? CleansiaColors.primary : CleansiaColors.outlineVariant
        let dotSize: CGFloat = entry.state == .done ? 14 : 12
        return HStack(alignment: .top, spacing: Spacing.s) {
            VStack(spacing: 0) {
                ZStack {
                    Circle()
                        .fill(dotColor)
                        .frame(width: dotSize, height: dotSize)
                    if entry.state == .done {
                        Image(systemName: "checkmark")
                            .font(.system(size: 8, weight: .bold))
                            .foregroundColor(CleansiaColors.onPrimary)
                    }
                }
                if !isLast {
                    Rectangle()
                        .fill(connectorColor)
                        .frame(width: 2, height: 22)
                }
            }
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(entry.step.title)
                    .font(CleansiaTypography.bodyMedium)
                    .fontWeight(entry.state == .active ? .semibold : .regular)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(entry.step.subtitle)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(.bottom, isLast ? 0 : Spacing.xs)
            Spacer(minLength: 0)
        }
    }
}

private struct SummaryRow: Identifiable {
    let label: String
    let value: String

    var id: String {
        label
    }
}

private extension View {
    func successCard() -> some View {
        background(CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }
}

#if DEBUG
    struct BookingSuccessView_Previews: PreviewProvider {
        static var previews: some View {
            BookingSuccessView(
                confirmationCode: "CLN-12345",
                orderId: "",
                loadOrder: { _ in nil },
                onViewOrder: {},
                onDone: {}
            )
        }
    }
#endif
