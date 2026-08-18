import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderDetailContent: View {
    let order: CustomerOrderDetail
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
                    // One hero block — the headline names the phase and the bar draws it — but not
                    // touching: at zero the label sat ON the bar. Spacing.xs is the smallest gap that
                    // reads as "these belong together" rather than "these are one control".
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        OrderStatusHero(order: order)
                        CustomerOrderTrackerHero(status: status)
                    }
                    OrderHeroFactsStrip(order: order)
                        // The bar is a thin 4pt rule; the parent's Spacing.s alone left the facts row
                        // crowding it from below.
                        .padding(.top, Spacing.xxs)

                    if let disclosure = PreferredOfferPresentation.disclosure(for: order) {
                        PreferredOfferCard(disclosure: disclosure)
                    }

                    if let address = order.address {
                        OrderAddressCard(address: address)
                    }

                    CleaningDetailsCard(order: order)

                    if !order.services.isEmpty {
                        OrderServicesCard(services: order.services)
                    }

                    if !order.packages.isEmpty {
                        OrderPackagesCard(packages: order.packages)
                    }

                    OrderInstructionsCard(order: order)

                    if let gallery = photos.loadedResponse, !gallery.photos.isEmpty {
                        OrderPhotosSection(gallery: gallery, onViewPhotos: onViewPhotos)
                    }

                    if !order.assignedEmployees.isEmpty {
                        AssignedCleanersCard(employees: order.assignedEmployees)
                    }

                    OrderPriceBreakdownCard(order: order)

                    if !order.statusHistory.isEmpty {
                        OrderTimelineCard(history: order.statusHistory)
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

/// The identity strip pinned to the top of the sheet — order number, status and when. It never scrolls,
/// so collapsing the sheet onto the map still leaves the customer looking at which order this is.
///
/// **Two left-aligned lines, and the trailing half deliberately empty.** The mascot puck rides the
/// sheet's top edge at `.trailing` (see `SnapSheet`'s ornament), so its lower half lands on precisely
/// this row — and while the date sat on the right of a single line, that is what it landed on. Stacking
/// number-and-pill over the date moves both clear of it. Everything that used to compete for the right
/// half (price, code) is in `OrderHeroFactsStrip` below, inside the scroll view.
struct OrderDetailCompactHeader: View {
    @Environment(\.locale) private var locale
    let order: CustomerOrderDetail

    var body: some View {
        HStack(alignment: .top) {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: Spacing.xs) {
                    if let number = order.displayOrderNumber, !number.isBlank {
                        Text("#\(number)")
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                    }
                    OrderStatusPill(code: order.statusCode)
                }
                Label(
                    OrdersFormat.dateTime(order.cleaningDateTime, locale: locale),
                    systemImage: "calendar"
                )
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
            }
            Spacer(minLength: mascotClearance)
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.bottom, Spacing.xs)
    }
}

/// How much of this row's trailing edge the mascot puck occupies: half its 128pt width plus the trailing
/// inset `SnapSheet` gives it. Reserved rather than merely avoided, so a long date shrinks instead of
/// sliding under the character.
private let mascotClearance: CGFloat = 80

/// The customer's five-phase tracker, drawn from `CleansiaCore` off this app's own status enum.
///
/// Rendered for EVERY status, which is the point: before this, an order that was merely booked or
/// already finished showed no phase indicator at all. `LiveProgress.activeStep` is reused as the index
/// rather than a second mapping being written beside it — it already answers exactly this question.
struct CustomerOrderTrackerHero: View {
    @Environment(\.locale) private var locale
    let status: OrderStatus?

    var body: some View {
        OrderTrackerBar(
            state: OrderTrackerRule.state(
                step: LiveProgress.activeStep(for: status)?.rawValue ?? 0,
                cancelled: status == ._6,
                completed: status == ._5
            ),
            cancelledLabel: OrderStatusPresentation.label(Code(value: 6))
        )
        .id(locale.identifier)
    }
}
