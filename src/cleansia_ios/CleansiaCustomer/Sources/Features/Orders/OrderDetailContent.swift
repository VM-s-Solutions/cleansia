import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderDetailContent: View {
    @Environment(\.locale) private var locale
    let order: CustomerOrderDetail
    let markets: MarketState
    let photos: PhotosUiState
    let isDownloadingReceipt: Bool
    let onLeaveReview: () -> Void
    let onDownloadReceipt: () -> Void
    let onViewPhotos: () -> Void
    var onPreviewPhoto: ((OrderPhotoPhase, String?) -> Void)?

    private var status: OrderStatus? {
        order.status
    }

    var body: some View {
        VStack(spacing: 0) {
            OrderDetailCompactHeader(order: order)
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    // One hero block — the headline names the phase and the bar draws it — but not
                    // touching: at zero the label sat ON the bar. Spacing.xs is the smallest gap that
                    // reads as "these belong together" rather than "these are one control".
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        OrderStatusHero(order: order)
                        CustomerOrderTrackerHero(order: order)

                        // Why, not just that. The cancellation push tells the customer to tap for the
                        // reason, and until now the screen they landed on had none — a failed payment
                        // had to be inferred from the absence of a charge. Present only for
                        // platform-initiated cancellations; a customer who cancelled their own booking
                        // does not need to be told they did.
                        if let reason = CancellationReasonCopy.text(for: order.systemCancellationReason) {
                            Text(reason)
                                .font(CleansiaTypography.bodyMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .fixedSize(horizontal: false, vertical: true)
                                .padding(.top, Spacing.xxs)
                        }

                        if order.isGuestBooking {
                            Text(L10n.OrderDetail.guestBookingNote)
                                .font(CleansiaTypography.bodyMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .fixedSize(horizontal: false, vertical: true)
                                .padding(.top, Spacing.xxs)
                        }
                    }
                    OrderHeroFactsStrip(order: order, marketLabel: OrderMarketLabel.text(
                        countryId: order.countryId, currencyCode: order.currencyCode,
                        markets: markets, locale: locale
                    ))
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
                        OrderPhotosSection(gallery: gallery, onViewPhotos: onViewPhotos, onPreviewPhoto: onPreviewPhoto)
                    }

                    if !order.assignedEmployees.isEmpty {
                        AssignedCleanersCard(employees: order.assignedEmployees)
                    }

                    OrderPriceBreakdownCard(order: order)

                    if !order.statusHistory.isEmpty {
                        OrderTimelineCard(history: order.statusHistory)
                    }

                    if order.offersReview {
                        if let review = order.review {
                            OrderReviewCard(review: review, onLeaveReview: onLeaveReview)
                        }
                    }

                    if order.offersReceipt {
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
                        Text(verbatim: "#\(number)")
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                    }
                    OrderStatusPill(code: order.statusCode)
                }
                OrderCalendarLine(OrdersFormat.dateTime(order.cleaningDateTime, locale: locale))
                if order.isGuestBooking {
                    GuestBookingLabel()
                }
            }
            Spacer(minLength: mascotClearance)
        }
        .fixedSize(horizontal: false, vertical: true)
        .padding(.horizontal, Spacing.ml)
        .padding(.bottom, Spacing.xs)
        .background {
            GeometryReader { geometry in
                Color.clear.preference(key: OrderDetailHeaderHeight.self, value: geometry.size.height)
            }
        }
    }
}

struct OrderDetailHeaderHeight: PreferenceKey {
    static var defaultValue: CGFloat = 0

    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

/// How much of this row's trailing edge the mascot puck occupies: half its 128pt width plus the trailing
/// inset `SnapSheet` gives it. Reserved rather than merely avoided, so a long date wraps instead of
/// sliding under the character.
private let mascotClearance: CGFloat = 80

/// The customer's five-phase tracker, drawn from `CleansiaCore` off this app's own status enum.
///
/// Rendered for EVERY status, which is the point: before this, an order that was merely booked or
/// already finished showed no phase indicator at all. `LiveProgress.activeStep` is reused as the index
/// rather than a second mapping being written beside it — it already answers exactly this question.
struct CustomerOrderTrackerHero: View {
    @Environment(\.locale) private var locale
    let order: CustomerOrderDetail

    private var status: OrderStatus? {
        order.status
    }

    var body: some View {
        TimelineView(.periodic(from: .now, by: 30)) { context in
            let progress = status == ._4 ? LiveProgress.estimateProgress(
                history: order.statusHistory, estimatedMinutes: order.estimatedMinutes, now: context.date
            ) : nil
            VStack(alignment: .leading, spacing: Spacing.xs) {
                OrderTrackerBar(
                    state: OrderTrackerRule.state(
                        step: LiveProgress.activeStep(for: status)?.rawValue ?? 0,
                        cancelled: status == ._6,
                        completed: status == ._5
                    ),
                    cancelledLabel: OrderStatusPresentation.label(Code(value: 6)),
                    currentFraction: progress?.fraction,
                    isOverEstimate: (progress?.overrunMinutes ?? 0) > 0
                )
                if let progress {
                    if progress.overrunMinutes > 0 {
                        Label(
                            L10n.OrderDetail.estimateOverrun(progress.overrunMinutes),
                            systemImage: "clock.badge.exclamationmark"
                        )
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.pendingInk)
                    } else {
                        Text(L10n.OrderDetail.estimateRemaining(progress.remainingMinutes))
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
            }
        }
        .id(locale.identifier)
    }
}
