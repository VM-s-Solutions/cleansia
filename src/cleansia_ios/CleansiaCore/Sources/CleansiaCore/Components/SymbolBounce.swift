import SwiftUI

/// The SF Symbol bounce (iOS 17+) on the few symbol moments that carry meaning, behind one
/// `#available` gate. Before iOS 17 the symbol stays still, which is how it has always looked; under
/// Reduce Motion the system tones the effect down by itself.
public extension View {
    /// Bounces once each time `count` rises — a new unread notification, not one being read.
    func cleansiaBounce(onIncreaseOf count: Int) -> some View {
        modifier(SymbolBounce(count: count))
    }

    /// Bounces once as the symbol appears — a success mark.
    func cleansiaBounceOnAppear() -> some View {
        modifier(SymbolBounce(count: nil))
    }
}

private struct SymbolBounce: ViewModifier {
    /// `nil`: bounce on appear instead.
    let count: Int?
    @State private var bounces = 0
    @State private var lastCount: Int?

    func body(content: Content) -> some View {
        bouncing(content)
            .onAppear {
                if count == nil { bounces += 1 }
                lastCount = count
            }
            .onChange(of: count) { newCount in
                if let newCount, let lastCount, newCount > lastCount { bounces += 1 }
                lastCount = newCount
            }
    }

    @ViewBuilder
    private func bouncing(_ content: Content) -> some View {
        if #available(iOS 17, *) {
            content.symbolEffect(.bounce, value: bounces)
        } else {
            content
        }
    }
}
