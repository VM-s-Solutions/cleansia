import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The before/after gallery for one order.
///
/// **Refuse the counts.** They are the server's own figures — `GetOrderPhotos` runs a dedicated count
/// query per photo type rather than tallying the list beside them — so they are a second statement
/// about the same order, not a summary of the array. Coerced, the rail's pills read "0 before photos"
/// above a row that is plainly showing them, which is the tell that a count and a rail have become two
/// sources for one fact. A genuine zero still arrives as zero; only a missing figure refuses.
///
/// **Keep every row the server sent, and do not drop the unusable ones.** This is the §D2 question
/// answered for this surface and it comes out the other way from the orders list: dropping a row here
/// would make the rail disagree with the counts by exactly the number dropped, which is the same
/// disagreement the refusal above exists to close. A row whose `blobUrl` did not arrive renders as the
/// placeholder tile it already renders as — a picture that failed to load, which is what it is.
///
/// The list itself keeps `?? []`: an absent gallery and an empty one are the same fact to a screen that
/// sums nothing, and the counts do not come from it.
struct OrderPhotos: Equatable {
    let photos: [GetOrderPhotosOrderPhotoDto]
    let beforeCount: Int
    let afterCount: Int
}

extension OrderPhotos {
    init(_ response: GetOrderPhotosResponse) throws {
        photos = response.photos ?? []
        beforeCount = try response.beforePhotoCount.require("beforePhotoCount")
        afterCount = try response.afterPhotoCount.require("afterPhotoCount")
    }
}
