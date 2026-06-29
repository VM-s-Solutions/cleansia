import Foundation

#if canImport(StripePaymentSheet) && canImport(UIKit)
    import StripeCore
    import StripePaymentSheet
    import UIKit

    enum StripeLaunch {
        static func applyPublishableKey() {
            guard StripeConfig.isCardPaymentAvailable else { return }
            STPAPIClient.shared.publishableKey = StripeConfig.publishableKey
        }
    }

    @MainActor
    final class StripePaymentController: PaymentSheetPresenting {
        func present(_ presentation: PaymentSheetPresentation) async -> PaymentSheetOutcome {
            guard !presentation.clientSecret.isEmpty else { return .failed }
            guard let presenter = Self.topViewController() else { return .failed }

            var configuration = PaymentSheet.Configuration()
            configuration.merchantDisplayName = presentation.merchantDisplayName
            configuration.allowsDelayedPaymentMethods = false
            if !presentation.stripeCustomerId.isEmpty, !presentation.ephemeralKey.isEmpty {
                configuration.customer = .init(
                    id: presentation.stripeCustomerId,
                    ephemeralKeySecret: presentation.ephemeralKey
                )
            }

            let sheet = PaymentSheet(
                paymentIntentClientSecret: presentation.clientSecret,
                configuration: configuration
            )

            return await withCheckedContinuation { continuation in
                sheet.present(from: presenter) { result in
                    switch result {
                    case .completed:
                        continuation.resume(returning: .completed)
                    case .canceled:
                        continuation.resume(returning: .canceled)
                    case .failed:
                        continuation.resume(returning: .failed)
                    }
                }
            }
        }

        private static func topViewController() -> UIViewController? {
            let scene = UIApplication.shared.connectedScenes
                .compactMap { $0 as? UIWindowScene }
                .first { $0.activationState == .foregroundActive }
            var top = scene?.windows.first { $0.isKeyWindow }?.rootViewController
            while let presented = top?.presentedViewController {
                top = presented
            }
            return top
        }
    }

#else

    enum StripeLaunch {
        static func applyPublishableKey() {}
    }

    @MainActor
    final class StripePaymentController: PaymentSheetPresenting {
        func present(_: PaymentSheetPresentation) async -> PaymentSheetOutcome {
            .failed
        }
    }

#endif
