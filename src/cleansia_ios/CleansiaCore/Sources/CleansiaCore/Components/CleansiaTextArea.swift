import SwiftUI

/// Multiline free-text input — the sibling of `CleansiaTextField`, whose
/// parameter naming and order it deliberately mirrors so the two read alike at
/// call sites.
///
/// The visual treatment is lifted verbatim from the customer app's dispute
/// form (`CreateDisputeView`), the one hand-rolled `TextEditor` in the tree that
/// had the full set: a placeholder overlay aligned to the editor's first line, a
/// clamped length with a counter, and a rounded container painted *behind* the
/// editor. Rather than a fifth hand-roll, that treatment now lives here once.
///
/// Unlike `CleansiaTextField` the label does not float: a text area is tall
/// enough that a label animating into a caption above it reads as jitter, and
/// the four sites this replaces all used a static caption.
public struct CleansiaTextArea: View {
    @Binding private var value: String
    private let label: String?
    private let placeholder: String?
    private let helper: String?
    private let errorText: String?
    private let maxLength: Int?
    private let showsCounter: Bool
    private let minHeight: CGFloat
    private let enabled: Bool
    private let transparentContainer: Bool

    public init(
        value: Binding<String>,
        label: String? = nil,
        placeholder: String? = nil,
        helper: String? = nil,
        errorText: String? = nil,
        maxLength: Int? = nil,
        showsCounter: Bool = false,
        minHeight: CGFloat = 96,
        enabled: Bool = true,
        transparentContainer: Bool = false
    ) {
        _value = value
        self.label = label
        self.placeholder = placeholder
        self.helper = helper
        self.errorText = errorText
        self.maxLength = maxLength
        self.showsCounter = showsCounter
        self.minHeight = minHeight
        self.enabled = enabled
        self.transparentContainer = transparentContainer
    }

    /// Truncates `text` to `maxLength` **characters** (grapheme clusters), which
    /// is what every site this component replaces already did via
    /// `String.prefix`. Deliberately not UTF-8 byte length: the server limits
    /// these fields by character count too, and a byte clamp would cut a
    /// Cyrillic or emoji input short of the advertised counter.
    ///
    /// A `nil` limit is the identity. Exposed so the clamp is testable without a
    /// snapshot harness — the view itself has no other logic worth asserting.
    public static func clamped(_ text: String, to maxLength: Int?) -> String {
        guard let maxLength, text.count > maxLength else { return text }
        return String(text.prefix(maxLength))
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            if let label {
                Text(label)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            editor
            if let supporting = errorText ?? helper {
                Text(supporting)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(isError ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)
            }
            if showsCounter, let maxLength {
                Text("\(value.count)/\(maxLength)")
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity, alignment: .trailing)
            }
        }
    }

    private var editor: some View {
        ZStack(alignment: .topLeading) {
            if let placeholder, value.isEmpty {
                // These insets are what put the placeholder's first line exactly
                // where the editor's caret sits (the editor's own text is inset
                // by UITextView's internal padding on top of its `.padding`).
                // Do not "tidy" them into a single value.
                Text(placeholder)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.horizontal, Spacing.m)
                    .padding(.vertical, 12)
            }
            TextEditor(text: $value)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurface)
                // Without this the backing UITextView paints its own opaque
                // background over the container colour — very visible in dark mode.
                .scrollContentBackground(.hidden)
                .frame(minHeight: minHeight)
                .padding(.horizontal, Spacing.s)
                .padding(.vertical, 6)
                .disabled(!enabled)
                .onChange(of: value) { next in
                    // The clamp lives here, once. Callers pass `maxLength:` and
                    // must not also clamp — two writers make `onChange` re-fire
                    // and the caret jump to the end.
                    let capped = Self.clamped(next, to: maxLength)
                    if capped != next { value = capped }
                }
        }
        .background(containerColor, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(isError ? CleansiaColors.error : CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }

    private var isError: Bool {
        errorText != nil
    }

    private var containerColor: Color {
        transparentContainer ? .clear : CleansiaColors.surface
    }
}

#if DEBUG
    struct CleansiaTextArea_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper("") { binding in
                VStack(spacing: Spacing.m) {
                    CleansiaTextArea(
                        value: binding,
                        label: "Note",
                        placeholder: "Describe what happened",
                        maxLength: 120,
                        showsCounter: true,
                        minHeight: 120
                    )
                    CleansiaTextArea(
                        value: .constant("Something went wrong here"),
                        label: "Issue",
                        errorText: "Please add more detail"
                    )
                    CleansiaTextArea(value: .constant("Read-only"), label: "Disabled", enabled: false)
                }
                .padding()
                .background(CleansiaColors.background)
            }
        }
    }
#endif
