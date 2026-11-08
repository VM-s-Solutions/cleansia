import CleansiaCore
import SwiftUI

/// What a booking still needs, a row per step: the open step carries its field and the cleaner's view of it, and the
/// optional birth date comes last.
struct OnboardingReadinessCard: View {
    let checklist: OnboardingChecklist
    let email: String
    let emailHidden: Bool
    let birthDate: Date?
    @Binding var openStep: OnboardingStep
    @Binding var openedByHand: Bool
    @Binding var firstName: String
    @Binding var lastName: String
    @Binding var phone: String
    let onBirthDate: () -> Void

    @Environment(\.locale) private var locale

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            head
            Rectangle().fill(CleansiaColors.outlineVariant).frame(height: 1)
            nameRow
            InsetRule()
            LineRow(label: L10n.EditProfile.email, state: checklist.email ? .done : .waiting) {
                if emailHidden {
                    Text(L10n.Onboarding.emailHidden)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                } else {
                    EmailValue(email)
                }
            }
            InsetRule()
            phoneRow
            InsetRule()
            TapRow(
                label: L10n.Onboarding.birthDateLabel,
                value: birthDate.map { CalendarDay.text($0, locale: locale) },
                note: L10n.Onboarding.birthDateHelper,
                state: birthDate == nil ? .waiting : .done,
                action: onBirthDate
            )
        }
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .strokeBorder(CleansiaColors.outline, lineWidth: 1)
        )
    }

    private var head: some View {
        VStack(alignment: .leading, spacing: Spacing.hair) {
            Text(checklist.isReady ? L10n.Onboarding.ready : L10n.Onboarding.progress(checklist.doneCount, of: 3))
                .cleansiaFont(CleansiaTypography.headlineSmall)
                .foregroundColor(checklist.isReady ? CleansiaColors.primaryText : CleansiaColors.onSurface)
            Text(prompt)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, Spacing.s)
        .padding(.horizontal, Spacing.m)
        .accessibilityElement(children: .combine)
    }

    private var prompt: String {
        switch checklist.prompt {
        case .ready: L10n.Onboarding.readySub
        case .addName: L10n.Onboarding.leftName
        case .addPhone: L10n.Onboarding.leftPhone
        case .addBoth: L10n.Onboarding.leftBoth
        }
    }

    @ViewBuilder private var nameRow: some View {
        if openStep == .name {
            OpenRow(label: L10n.Onboarding.rowName, done: checklist.name) {
                HStack(alignment: .top, spacing: Spacing.s) {
                    CleansiaTextField(
                        value: $firstName,
                        label: L10n.Onboarding.firstNameLabel,
                        textContentType: .givenName,
                        autoFocus: openedByHand,
                        labelAbove: true
                    )
                    CleansiaTextField(
                        value: $lastName,
                        label: L10n.Onboarding.lastNameLabel,
                        textContentType: .familyName,
                        labelAbove: true,
                        onNext: {
                            openedByHand = true
                            openStep = .phone
                        }
                    )
                }
                CleanerViewExample(name: typedName, phone: phone, reachable: checklist.phone)
            }
        } else if checklist.name {
            LineRow(label: L10n.Onboarding.rowName, state: .done) { RowValue(text: typedName) }
        } else {
            TapRow(label: L10n.Onboarding.rowName, value: nil, note: nil, state: .waiting) {
                openedByHand = true
                openStep = .name
            }
        }
    }

    @ViewBuilder private var phoneRow: some View {
        if openStep == .phone {
            OpenRow(label: L10n.Onboarding.phoneLabel, done: checklist.phone) {
                // shortcut: Czech numbers only, the one market served today;
                // pass the market's country when a second one opens.
                CleansiaPhoneInput(
                    value: $phone,
                    label: L10n.Onboarding.phoneLabel,
                    countryRegion: "CZ",
                    autoFocus: openedByHand
                )
                CleanerViewExample(name: typedName, phone: phone, reachable: checklist.phone)
            }
        } else if checklist.phone {
            LineRow(label: L10n.Onboarding.phoneLabel, state: .done) {
                RowValue(text: PhoneMaskEngine.czech.reformat(phone).text)
            }
        } else {
            TapRow(label: L10n.Onboarding.phoneLabel, value: nil, note: nil, state: .waiting) {
                openedByHand = true
                openStep = .phone
            }
        }
    }

    /// The done Name row and the example both show it, in either step, so the example follows her typing.
    private var typedName: String {
        [firstName, lastName].map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
            .joined(separator: " ")
    }
}

/// A long address gives way in its local part and keeps its domain whole; VoiceOver reads it once, whole.
struct EmailValue: View {
    let email: String

    init(_ email: String) {
        self.email = email
    }

    static func parts(_ email: String) -> (local: Substring, domain: Substring) {
        guard let atSign = email.lastIndex(of: "@") else { return (email[...], "") }
        return (email[..<atSign], email[atSign...])
    }

    var body: some View {
        let parts = Self.parts(email)
        HStack(spacing: 0) {
            Text(verbatim: String(parts.local))
                .lineLimit(1)
                .truncationMode(.tail)
            Text(verbatim: String(parts.domain))
                .lineLimit(1)
                .fixedSize()
        }
        .font(CleansiaTypography.bodyLarge)
        .foregroundColor(CleansiaColors.onSurface)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(Text(verbatim: email))
    }
}

private enum RowState {
    case done
    case current
    case waiting

    var spoken: String {
        switch self {
        case .done: L10n.Onboarding.rowDone
        case .current: L10n.Onboarding.rowCurrent
        case .waiting: L10n.Onboarding.rowWaiting
        }
    }
}

private struct RowMark: View {
    let state: RowState

    var body: some View {
        ZStack {
            switch state {
            case .done:
                Circle().fill(CleansiaColors.primary)
                Image(systemName: "checkmark")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundColor(CleansiaColors.onPrimary)
            case .current:
                Circle().strokeBorder(CleansiaColors.primary, lineWidth: 2.5)
                Circle().fill(CleansiaColors.primary).frame(width: 8, height: 8)
            case .waiting:
                Circle().strokeBorder(CleansiaColors.quiet, lineWidth: 2)
            }
        }
        .frame(width: 24, height: 24)
        .accessibilityHidden(true)
    }
}

private struct RowLabel: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.labelLarge)
            .foregroundColor(CleansiaColors.quiet)
            .lineLimit(1)
            .layoutPriority(1)
    }
}

private struct RowValue: View {
    let text: String

    var body: some View {
        Text(verbatim: text)
            .font(CleansiaTypography.bodyLarge)
            .foregroundColor(CleansiaColors.onSurface)
            .lineLimit(1)
    }
}

/// A done step: one line, not a tap target.
private struct LineRow<Value: View>: View {
    let label: String
    let state: RowState
    @ViewBuilder let value: Value

    var body: some View {
        HStack(spacing: Spacing.s) {
            RowMark(state: state)
            RowLabel(text: label)
            value.frame(maxWidth: .infinity, alignment: .trailing)
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.xs)
        .frame(minHeight: 44)
        // Without a shape the element is only as tall as its words, and VoiceOver's frame should be the row.
        .contentShape(Rectangle())
        .accessibilityElement(children: .combine)
        .accessibilityValue(Text(state.spoken))
    }
}

/// A waiting step, or the birth date: the whole row is the target. "Not set" is not read, so the row's state is said
/// once.
private struct TapRow: View {
    let label: String
    let value: String?
    let note: String?
    let state: RowState
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                HStack(spacing: Spacing.s) {
                    RowMark(state: state)
                    RowLabel(text: label)
                    Group {
                        if let value {
                            RowValue(text: value)
                        } else {
                            Text(L10n.Onboarding.notSet)
                                .font(CleansiaTypography.bodyLarge)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .lineLimit(1)
                                .accessibilityHidden(true)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .trailing)
                    Image(systemName: "chevron.right")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(CleansiaColors.quiet)
                        .frame(width: 16, height: 16)
                        .accessibilityHidden(true)
                }
                if let note {
                    Text(note)
                        .font(CleansiaTypography.bodySmall)
                        .foregroundColor(CleansiaColors.quiet)
                        .padding(.leading, 24 + Spacing.s)
                }
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.xs)
            .frame(minHeight: 44)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityValue(Text(state.spoken))
    }
}

/// The open step: its head is one element, and its field and the example stay their own.
private struct OpenRow<Content: View>: View {
    let label: String
    let done: Bool
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(spacing: Spacing.s) {
                RowMark(state: done ? .done : .current)
                Text(label)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityElement(children: .combine)
            .accessibilityValue(Text(done ? L10n.Onboarding.rowDone : L10n.Onboarding.rowCurrent))
            content
        }
        .padding(.vertical, Spacing.s)
        .padding(.horizontal, Spacing.m)
    }
}

private struct InsetRule: View {
    var body: some View {
        Rectangle()
            .fill(CleansiaColors.outlineVariant)
            .frame(height: 1)
            .padding(.leading, Spacing.m + 24 + Spacing.s)
    }
}

/// The customer card her cleaner sees, filled from what she types. The chips are pictures, not buttons, and a chip
/// her number cannot reach yet is a dashed slot that is not read.
private struct CleanerViewExample: View {
    let name: String
    let phone: String
    let reachable: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Spacing.xs) {
                Text(L10n.Onboarding.cardTitle)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Spacer(minLength: 0)
                Text(L10n.Onboarding.cardExample)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.horizontal, Spacing.xs)
                    .frame(minHeight: 20)
                    .background(CleansiaColors.surface, in: Capsule())
            }
            line(icon: "person") {
                if name.isEmpty {
                    Dash()
                } else {
                    Text(verbatim: name)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
            line(icon: "mappin.and.ellipse") {
                Text(L10n.Onboarding.sampleAddress)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            ChipFlow(spacing: 6) {
                ExampleChip(title: L10n.Onboarding.cardCall, icon: "phone", isOn: reachable)
                ExampleChip(title: L10n.Onboarding.cardSms, icon: "message", isOn: reachable)
                ExampleChip(title: L10n.Onboarding.cardNavigate, icon: "map", isOn: true)
            }
            .padding(.top, Spacing.xs)
            Rectangle().fill(CleansiaColors.outline).frame(height: 1).padding(.top, Spacing.xs)
            dial.padding(.top, Spacing.xs)
        }
        // xxs, not the mock's 10: with xs the 375 x 812 phone scrolls by 6 pt.
        .padding(.vertical, Spacing.xxs)
        .padding(.horizontal, Spacing.s)
        .background(CleansiaColors.surfaceVariant, in: RoundedRectangle(cornerRadius: CornerRadius.small))
        .accessibilityElement(children: .combine)
    }

    private var dial: some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
            Text(L10n.Onboarding.cardDial)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer(minLength: 0)
            if phone.isEmpty {
                Dash()
            } else {
                Text(verbatim: PhoneMaskEngine.czech.reformat(phone).text)
                    .font(CleansiaTypography.titleMedium)
                    .monospacedDigit()
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize()
            }
        }
    }

    private func line(icon: String, @ViewBuilder content: () -> some View) -> some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: icon)
                .font(.system(size: 16))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .frame(width: 16, height: 16)
                .accessibilityHidden(true)
            content()
        }
        .padding(.top, Spacing.xxs)
    }
}

private struct Dash: View {
    var body: some View {
        Text(verbatim: "—")
            .font(CleansiaTypography.bodyLarge)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .accessibilityLabel(Text(L10n.Onboarding.notSet))
    }
}

private struct ExampleChip: View {
    let title: String
    let icon: String
    let isOn: Bool

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: icon)
                .font(.system(size: 16))
                .accessibilityHidden(true)
            Text(title)
                .font(CleansiaTypography.labelLarge)
        }
        .foregroundColor(isOn ? CleansiaColors.primaryText : CleansiaColors.onSurfaceVariant)
        .padding(.horizontal, Spacing.xs)
        .frame(minHeight: 34)
        .background(isOn ? CleansiaColors.primary.opacity(0.12) : Color.clear, in: Capsule())
        .overlay {
            if !isOn {
                Capsule().strokeBorder(CleansiaColors.quiet, style: StrokeStyle(lineWidth: 1, dash: [4, 3]))
            }
        }
        .accessibilityHidden(!isOn)
    }
}

private extension PhoneMaskEngine {
    /// The wire number as the example and the done row show it. The region is named rather than read from the
    /// handset, because iOS 16 reports a region override as "czzzzz", which formats nothing; every real region
    /// formats a number that starts with its `+` code alike.
    static let czech = PhoneMaskEngine(formatter: PhoneNumberKitDisplayFormatter(defaultRegion: "CZ"))
}
