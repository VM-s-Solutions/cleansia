import CleansiaCustomerApi
import Foundation

extension OpenAPIDateWithoutTime {
    /// The one way this app builds a calendar day for the wire.
    ///
    /// The generated initializer defaults `timezone` to the handset's and then ADDS that offset to the
    /// instant before formatting in GMT, so a day that arrived as midnight UTC goes back out as the
    /// previous day from anywhere west of Greenwich — and, because the next read lands on midnight UTC
    /// again, one more day on every save after that. Nothing catches it downstream: `==` compares only
    /// `wrappedDate`, which every initializer stores untouched.
    init?(day date: Date?) {
        guard let date else { return nil }
        self.init(wrappedDate: date, timezone: .gmt)
    }
}
