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
        onViewOrder: (() -> Void)?,
        onDone: @escaping () -> Void
    ) {
        self.confirmationCode = confirmationCode
        self.onViewOrder = onViewOrder
        self.onDone = onDone
        _orderVM = StateObject(wrappedValue: BookingSuccessViewModel(orderId: orderId, fetch: loadOrder))
    }

    var body: some View {
        VStack(spacing: Spacing.l) {
            Spacer(minLength: Spacing.l)
            AnimatedMascotView(.welcoming, loop: false, fallback: .waving)
                .frame(width: 220, height: 220)
            VStack(spacing: Spacing.s) {
                Text(L10n.Booking.successTitle)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onBackground)
                    .multilineTextAlignment(.center)
                Text(L10n.Booking.successSubtitle)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
            }
            if !confirmationCode.isBlank {
                confirmationCard
            }
            if let order = orderVM.order {
                orderSummaryCard(order)
                progressRow(order.status)
            }
            Spacer(minLength: Spacing.l)
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
        .frame(maxWidth: .infinity)
        .frame(minHeight: 0, maxHeight: .infinity, alignment: .top)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await orderVM.load() }
    }

    private var confirmationCard: some View {
        VStack(spacing: Spacing.xxs) {
            Text(L10n.Booking.successConfirmationCode)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(confirmationCode)
                .font(CleansiaTypography.titleLarge)
                .fontWeight(.bold)
                .foregroundColor(CleansiaColors.primary)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }

    /// Arrival window, address and total, mirroring what Android's booking
    /// success shows. Each row drops out when its value is blank. No new copy:
    /// SF Symbols carry the meaning and the address section reuses OrderDetail's.
    private func orderSummaryCard(_ order: OrderItem) -> some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            summaryRow(
                icon: "calendar",
                text: OrdersFormat.dateRange(
                    order.cleaningDateTime,
                    estimatedMinutes: order.estimatedTime ?? 0,
                    locale: locale
                )
            )
            if let address = order.address, let line = addressLine(address) {
                summaryRow(icon: "mappin.and.ellipse", text: line)
            }
            if (order.totalPrice ?? 0) > 0 {
                HStack {
                    Image(systemName: "creditcard")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .frame(width: 22)
                    Text(L10n.Booking.summaryTotal)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Spacer()
                    Text(OrdersFormat.price(order.totalPrice ?? 0, currencyCode: order.currency?.code))
                        .font(CleansiaTypography.titleMedium)
                        .fontWeight(.semibold)
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }

    private func summaryRow(icon: String, text: String) -> some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: icon)
                .foregroundColor(CleansiaColors.primary)
                .frame(width: 22)
            Text(text)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurface)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private func addressLine(_ address: OrderAddress) -> String? {
        let parts = [address.street, [address.zipCode, address.city].compactMap { $0 }.joined(separator: " ")]
            .compactMap { $0 }
            .filter { !$0.isBlank }
        return parts.isEmpty ? nil : parts.joined(separator: ", ")
    }

    /// Five-dot progress mirroring the order-detail live indicator; steps up to
    /// and including the active one are filled.
    private func progressRow(_ status: OrderStatus?) -> some View {
        let activeIndex = LiveProgress.activeStep(for: status)?.rawValue
        return HStack(spacing: Spacing.xs) {
            ForEach(LiveProgressStep.allCases, id: \.rawValue) { step in
                let reached = activeIndex.map { step.rawValue <= $0 } ?? false
                Circle()
                    .fill(reached ? CleansiaColors.primary : CleansiaColors.outlineVariant)
                    .frame(width: 8, height: 8)
                if step != LiveProgressStep.allCases.last {
                    Rectangle()
                        .fill(reached ? CleansiaColors.primary : CleansiaColors.outlineVariant)
                        .frame(height: 2)
                        .frame(maxWidth: .infinity)
                }
            }
        }
        .padding(.horizontal, Spacing.s)
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
