#if canImport(StripePaymentSheet)
    import StripePaymentSheet
    import XCTest
    @testable import CleansiaCustomer

    /// Every sheet the app opens (booking, order payment, saved cards, membership) is configured here, and Stripe
    /// offers Link to a card-only intent unless the sheet turns it off.
    @MainActor
    final class StripePaymentControllerTests: XCTestCase {
        func testEverySheetTurnsLinkOff() {
            for kind in [PaymentIntentKind.payment, .setup] {
                let presentation = PaymentSheetPresentation(
                    clientSecret: "pi_secret",
                    ephemeralKey: "ek",
                    stripeCustomerId: "cus",
                    merchantDisplayName: "Cleansia",
                    intentKind: kind
                )

                XCTAssertEqual(StripePaymentController.configuration(for: presentation).link.display, .never, "\(kind)")
            }
        }
    }
#endif
