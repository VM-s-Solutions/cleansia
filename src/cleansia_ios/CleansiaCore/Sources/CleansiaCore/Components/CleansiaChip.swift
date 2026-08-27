import SwiftUI

/// A selectable pill. The one chip the customer app draws wherever it offers a short closed set of
/// choices — cancellation reasons, review tags. Compose `CleansiaChip` parity, geometry for geometry.
///
/// **Selection policy belongs to the caller.** Cancellation reasons are radio-style with tap-to-clear;
/// review tags are multi-select under a cap. Both are one line at the call site, and folding either in
/// here would make the component wrong for the other — which is exactly why the original private
/// version could not be shared.
///
/// Lay a row of these out with ``ChipFlow``, not a fixed n-per-row grid: the labels are localized into
/// five languages and a Czech or Ukrainian label is routinely half again the width of its English
/// original.
public struct CleansiaChip: View {
    private let label: String
    private let isSelected: Bool
    private let enabled: Bool
    private let action: () -> Void

    public init(
        label: String,
        isSelected: Bool,
        enabled: Bool = true,
        action: @escaping () -> Void
    ) {
        self.label = label
        self.isSelected = isSelected
        self.enabled = enabled
        self.action = action
    }

    public var body: some View {
        Button(action: action) {
            Text(label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(isSelected ? CleansiaColors.primary : CleansiaColors.onSurface)
                // Truncate rather than wrap — same rule as the buttons, same as the Android twin.
                .lineLimit(1)
                .padding(.horizontal, Spacing.s)
                .padding(.vertical, Spacing.xs)
                .background(
                    isSelected ? CleansiaColors.primary.opacity(0.12) : CleansiaColors.surface,
                    in: Capsule()
                )
                .overlay(
                    Capsule().stroke(
                        isSelected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: isSelected ? 1.5 : 1
                    )
                )
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        // isSelected has to be announced, not just tappable: a multi-select chip row read out as a
        // list of buttons leaves a VoiceOver user no way to tell which tags they already picked.
        .accessibilityAddTraits(isSelected ? [.isButton, .isSelected] : .isButton)
    }
}
