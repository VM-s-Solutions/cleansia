import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct OrderDetailContent: View {
    let order: OrderDetail

    private var showAccessCard: Bool {
        order.isAssignedToCurrentUser
            && !(order.accessInstructions ?? "").trimmingCharacters(in: .whitespaces).isEmpty
            && (order.status == ._3 || order.status == ._4)
    }

    private var showFromCustomerCard: Bool {
        !(order.customerNotes ?? "").trimmingCharacters(in: .whitespaces).isEmpty
            || !(order.specialInstructions ?? "").trimmingCharacters(in: .whitespaces).isEmpty
    }

    var body: some View {
        VStack(spacing: 0) {
            OrderDetailCompactHeader(order: order)
            ScrollView {
                VStack(spacing: Spacing.m) {
                    OrderTrackerHero(status: order.status)
                    OrderMetadataRow(order: order)
                    if showAccessCard, let access = order.accessInstructions {
                        AccessCard(instructions: access)
                    }
                    CustomerCard(order: order)
                    ScopeCard(order: order)
                    if showFromCustomerCard {
                        FromCustomerNotesCard(order: order)
                    }
                    // Disabled Photos placeholder so the Complete-blocked hint is
                    // meaningful once the lifecycle slice lands; capture arrives
                    // with photo upload.
                    PhotosPlaceholderSection()
                    PaymentCard(order: order)
                    // The lifecycle action footer, checklist, notes/issues, and
                    // status timeline land in later slices — their slots are here.
                }
                .padding(.horizontal, Spacing.m)
                .padding(.vertical, Spacing.m)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }
}

/// Always-visible header at the top of the sheet (never scrolls) — order #,
/// status pill, date, pay (the compact-header parity).
private struct OrderDetailCompactHeader: View {
    let order: OrderDetail

    var body: some View {
        HStack(alignment: .top) {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: Spacing.xs) {
                    Text("#\(order.orderNumber)")
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    OrderStatusPill(status: order.status)
                }
                Text(OrdersFormat.relativeDateTime(order.cleaningDateTime))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
            if let pay = order.pay, pay > 0 {
                Text(OrdersFormat.money(pay, symbol: order.currencySymbol))
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.primary)
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.bottom, Spacing.s)
    }
}

struct OrderStatusPill: View {
    let status: OrderStatus?

    var body: some View {
        Text(L10n.Orders.statusLabel(status))
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(tint)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, 2)
            .background(tint.opacity(0.14), in: Capsule())
    }

    private var tint: Color {
        switch status {
        case ._0, ._1: CleansiaColors.warningStar
        case ._2: CleansiaColors.primary
        case ._3, ._4: CleansiaColors.secondary
        case ._5: CleansiaColors.successText
        case ._6: CleansiaColors.error
        case .none: CleansiaColors.onSurfaceVariant
        }
    }
}

private struct OrderTrackerHero: View {
    let status: OrderStatus?

    private var step: Int {
        switch status {
        case ._2: 1
        case ._3: 2
        case ._4: 3
        case ._5: 4
        default: 0
        }
    }

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            ForEach(1 ... 4, id: \.self) { index in
                Capsule()
                    .fill(index <= step ? CleansiaColors.primary : CleansiaColors.outlineVariant)
                    .frame(height: 6)
            }
        }
    }
}

private struct OrderMetadataRow: View {
    let order: OrderDetail

    var body: some View {
        HStack {
            Label(OrdersFormat.relativeDateTime(order.cleaningDateTime), systemImage: "calendar")
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
        }
    }
}

#if DEBUG
    extension OrderDetail {
        static let preview = OrderDetail(
            id: "order-1",
            orderNumber: "ORD-2026-001",
            status: ._4,
            cleaningDateTime: Date(timeIntervalSinceNow: 3600),
            pay: 1200,
            currencyCode: "CZK",
            currencySymbol: "Kč",
            address: OrderDetailAddress(street: "Vinohradská 12", city: "Praha", zipCode: "120 00"),
            coordinate: Coordinate(latitude: 50.0755, longitude: 14.4378),
            customerName: "Jana Nováková",
            customerPhone: "+420 777 123 456",
            rooms: 3,
            bathrooms: 2,
            services: ["Standard clean", "Window clean"],
            packages: [OrderDetailPackage(name: "Deep clean", price: 800)],
            extras: ["inside-oven", "interior-windows"],
            customerNotes: "Cat is friendly.",
            specialInstructions: "Use the eco products under the sink.",
            accessInstructions: "Code 1234 at the gate.",
            payment: OrderDetailPayment(
                subtotal: 1400,
                total: 1200,
                tierDiscount: 200,
                membershipDiscount: nil,
                promoDiscount: nil,
                methodName: "Card",
                statusName: "Paid"
            ),
            isAssignedToCurrentUser: true,
            hasAfterPhotos: false,
            orderNotes: [],
            orderIssues: [],
            statusHistory: []
        )
    }

    struct OrderDetailContent_Previews: PreviewProvider {
        static var previews: some View {
            OrderDetailContent(order: .preview)
                .background(CleansiaColors.surface)
        }
    }
#endif
