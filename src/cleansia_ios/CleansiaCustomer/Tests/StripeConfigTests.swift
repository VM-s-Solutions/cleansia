import XCTest
@testable import CleansiaCustomer

final class StripeConfigTests: XCTestCase {
    func testPlaceholderKeyIsTreatedAsUnconfigured() {
        XCTAssertFalse(StripeConfig.isConfigured(publishableKey: "$(STRIPE_PUBLISHABLE_KEY)"))
    }

    func testEmptyKeyIsUnconfigured() {
        XCTAssertFalse(StripeConfig.isConfigured(publishableKey: ""))
        XCTAssertFalse(StripeConfig.isConfigured(publishableKey: "   "))
    }

    func testPublishableKeyIsConfigured() {
        XCTAssertTrue(StripeConfig.isConfigured(publishableKey: "pk_test_abc123"))
    }
}

final class PaymentSheetSecretRedactionTests: XCTestCase {
    func testPresentationDescriptionNeverLeaksSecrets() {
        let presentation = PaymentSheetPresentation(
            clientSecret: "pi_3LeakSecret_secret_shouldNotAppear",
            ephemeralKey: "ek_LeakEphemeral_shouldNotAppear",
            stripeCustomerId: "cus_LeakCustomer_shouldNotAppear",
            merchantDisplayName: "Cleansia"
        )

        let rendered = "\(presentation)" + presentation.debugDescription

        XCTAssertFalse(rendered.contains("pi_3LeakSecret_secret_shouldNotAppear"))
        XCTAssertFalse(rendered.contains("ek_LeakEphemeral_shouldNotAppear"))
        XCTAssertFalse(rendered.contains("cus_LeakCustomer_shouldNotAppear"))
        XCTAssertTrue(rendered.contains("Cleansia"))
    }
}
