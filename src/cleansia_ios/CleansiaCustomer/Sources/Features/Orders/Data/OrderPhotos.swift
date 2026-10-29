import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct OrderPhotos: Equatable {
    let photos: [GetOrderPhotosOrderPhotoDto]
    let beforeCount: Int
    let afterCount: Int
}

extension OrderPhotos {
    func previewIndex(for selection: OrderPhotoSelection) -> Int? {
        guard let id = selection.photoID, !id.isBlank else { return nil }
        return photos(for: selection.phase).firstIndex { $0.id == id }
    }

    func photos(for phase: OrderPhotoPhase) -> [GetOrderPhotosOrderPhotoDto] {
        photos.filter { (OrderPhotoPhase(rawValue: $0.photoType?.rawValue ?? 0) ?? .other) == phase }
    }

    var phases: [OrderPhotoPhase] {
        OrderPhotoPhase.allCases.filter { $0 == .before || $0 == .after || !photos(for: $0).isEmpty }
    }

    init(_ response: GetOrderPhotosResponse) throws {
        photos = response.photos ?? []
        beforeCount = try response.beforePhotoCount.require("beforePhotoCount")
        afterCount = try response.afterPhotoCount.require("afterPhotoCount")
    }
}

struct OrderPhotoSelection: Equatable {
    let phase: OrderPhotoPhase
    let photoID: String?
}

enum OrderPhotoPhase: Int, CaseIterable, Identifiable {
    case before = 1
    case after = 2
    case entrance = 3
    case other = 0

    var id: Int {
        rawValue
    }

    var label: String {
        switch self {
        case .before: L10n.OrderPhotos.before
        case .after: L10n.OrderPhotos.after
        case .entrance: L10n.OrderPhotos.entrance
        case .other: L10n.OrderPhotos.other
        }
    }
}
