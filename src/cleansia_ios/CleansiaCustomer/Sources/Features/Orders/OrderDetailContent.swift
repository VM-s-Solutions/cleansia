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
                    OrderReceiptCard(order: order, isDownloading: isDownloadingReceipt, onDownload: onDownloadReceipt)
                }

                Color.clear.frame(height: Spacing.xl)
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.xs)
        }
    }

    private var showReceipt: Bool {
        !(order.receiptNumber?.isBlank ?? true) || OrderStatusGroup.isCompleted(status)
    }
}
