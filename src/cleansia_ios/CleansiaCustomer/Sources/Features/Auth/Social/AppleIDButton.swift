import SwiftUI

#if canImport(AuthenticationServices) && canImport(UIKit)
    import AuthenticationServices
    import UIKit

    struct AppleIDButton: UIViewRepresentable {
        @Environment(\.colorScheme) private var colorScheme
        let action: () -> Void

        func makeCoordinator() -> Coordinator {
            Coordinator(action: action)
        }

        func makeUIView(context: Context) -> ASAuthorizationAppleIDButton {
            let button = ASAuthorizationAppleIDButton(
                authorizationButtonType: .signIn,
                authorizationButtonStyle: colorScheme == .dark ? .white : .black
            )
            button.addTarget(context.coordinator, action: #selector(Coordinator.tapped), for: .touchUpInside)
            return button
        }

        func updateUIView(_: ASAuthorizationAppleIDButton, context: Context) {
            context.coordinator.action = action
        }

        final class Coordinator {
            var action: () -> Void

            init(action: @escaping () -> Void) {
                self.action = action
            }

            @objc func tapped() {
                action()
            }
        }
    }

#else

    struct AppleIDButton: View {
        let action: () -> Void

        var body: some View {
            EmptyView()
        }
    }

#endif
