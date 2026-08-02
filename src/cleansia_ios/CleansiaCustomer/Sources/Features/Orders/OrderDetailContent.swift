import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderDetailContent: View {
    let order: OrderItem
    let photos: PhotosUiState
    let isDownloadingReceipt: Bool
    let onLeaveReview: () -> Void
    let onDownloadReceipt: () -> Void
    let onViewPhotos: () -> Void

    private var status: OrderStatus? {
        order.status
    }

    var body: some View {
        VStack(spacing: 0) {
            OrderDetailCompactHeader(order: order)
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.s) {
                    if LiveProgress.usesLiveHero(status) {
                        LiveProgressHero(order: order)
                    } else {
                        OrderHeroCard(order: order)
                    }

                    if let address = order.address {
                        OrderAddressCard(address: address)
                    }

                    CleaningDetailsCard(order: order)

                    if let services = order.selectedServices, !services.isEmpty {
                        OrderServicesCard(services: services)
                    }

                    if let packages = order.selectedPackages, !packages.isEmpty {
                        OrderPackagesCard(packages: packages)
                    }

                    OrderInstructionsCard(order: order)

                    if let response = photos.loadedResponse, !(response.photos ?? []).isEmpty {
                        OrderPhotosSection(response: response, onViewPhotos: onViewPhotos)
                    }

                    if let employees = order.assignedEmployees, !employees.isEmpty {
                        AssignedCleanersCard(employees: employees)
                    }

                    if let history = order.statusHistory, !history.isEmpty {
                        OrderTimelineCard(history: history)
                    }

                    if OrderStatusGroup.isCompleted(status) {
                        OrderReviewCard(review: order.review, onLeaveReview: onLeaveReview)
                    }

                    if showReceipt {
                        OrderReceiptCard(
                            order: order,
                            isDownloading: isDownloadingReceipt,
                            onDownload: onDownloadReceipt
                        )
                    }

                    Color.clear.frame(height: Spacing.xl)
                }
                .padding(.horizontal, Spacing.ml)
                .padding(.top, Spacing.xs)
            }
        }
    }

    private var showReceipt: Bool {
        !(order.receiptNumber?.isBlank ?? true) || OrderStatusGroup.isCompleted(status)
    }
}

/// The identity strip pinned to the top of the sheet — order number, status and
/// when. It never scrolls, so collapsing the sheet onto the map still leaves the
/// customer looking at which order this is (the partner screen's compact header).
struct OrderDetailCompactHeader: View {
    @Environment(\.locale) private var locale
    let order: OrderItem

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
            if let number = order.displayOrderNumber, !number.isBlank {
                Text("#\(number)")
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            OrderStatusPill(
                label: OrderStatusPresentation.label(order.orderStatus),
                color: OrderStatusPresentation.color(order.orderStatus)
            )
            Spacer(minLength: Spacing.xs)
            Text(OrdersFormat.dateTime(order.cleaningDateTime, locale: locale))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.bottom, Spacing.xs)
    }
}
