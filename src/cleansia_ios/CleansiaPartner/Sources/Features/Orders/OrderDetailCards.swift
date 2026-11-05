import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UniformTypeIdentifiers

/// The partner's secondary action: a capsule outlined in the outline, 40 high in a 44 tap target, its word in the
/// text ink and only its icon in the tone. The contacts, Note and Issue are drawn with it, Issue in the error tone.
/// It presses and dims as the outlined button does.
struct TonalChip: View {
    enum Tone {
        case primary
        case error

        var icon: Color {
            self == .error ? CleansiaColors.error : CleansiaColors.primary
        }
    }

    static let label = CleansiaTextStyle.nunito(.semibold, size: 14)
    static let ink = CleansiaColors.onSurface

    @Environment(\.dynamicTypeSize) private var typeSize
    let title: String
    let icon: String
    var tone = Tone.primary
    /// Takes the width it is offered, its face centred in it.
    var fills = false
    var action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Image(systemName: icon)
                    .font(CleansiaFont.nunito(.semibold, size: 16))
                    .foregroundColor(tone.icon)
                    .frame(width: Self.iconBox(typeSize))
                    .capBandAligned(of: Self.label)
                    .accessibilityHidden(true)
                Text(title).font(CleansiaFont.nunito(.semibold, size: 14)).lineLimit(1).fixedSize()
            }
            .foregroundColor(Self.ink)
            .padding(.horizontal, 10)
            .frame(maxWidth: fills ? .infinity : nil, minHeight: 40)
            .overlay(Capsule().strokeBorder(CleansiaColors.outline, lineWidth: 1))
            .padding(.vertical, 2)
            .contentShape(Rectangle())
        }
    }

    /// The icon's box: the design's 18 at the default text size, grown with the type as the glyph in it is.
    static func iconBox(_ typeSize: DynamicTypeSize) -> CGFloat {
        let traits = UITraitCollection(preferredContentSizeCategory: UIContentSizeCategory(typeSize))
        return UIFontMetrics.default.scaledValue(for: 18, compatibleWith: traits)
    }
}

/// A fact flagged on a job: its icon on the first line's cap band and its words, on a tint of its own ink. Amber for
/// the cash to collect, red for an overlap, neutral for the rest.
struct TintedFlag: View {
    enum Tone {
        case neutral
        case amber
        case error
    }

    let icon: String
    let text: String
    var tone = Tone.neutral

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Image(systemName: icon)
                .font(CleansiaTypography.bodyMedium)
                .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                .accessibilityHidden(true)
            let parts = OrdersBoardRules.flagParts(text)
            if parts.count > 1 {
                OrdersDotFlow(items: parts.map { Text($0) })
            } else {
                Text(parts.first ?? text)
                    .font(CleansiaTypography.bodyMedium)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .foregroundColor(ink)
        .padding(.horizontal, 10)
        .padding(.vertical, Spacing.xxs)
        .background(fill, in: RoundedRectangle(cornerRadius: CornerRadius.small))
        .accessibilityElement(children: .combine)
    }

    private var ink: Color {
        switch tone {
        case .neutral: CleansiaColors.onSurfaceVariant
        case .amber: CleansiaColors.pendingInk
        case .error: CleansiaColors.error
        }
    }

    private var fill: Color {
        switch tone {
        case .neutral: CleansiaColors.surfaceVariant
        case .amber: CleansiaColors.pendingInk.opacity(0.12)
        case .error: CleansiaColors.error.opacity(0.12)
        }
    }
}

/// Navigate, Call and SMS as equal chips, the Active hero's and the run sheet's, each with its icon. When one
/// does not fit its third, Navigate takes its own row over Call and SMS, then one column. Navigate needs a street;
/// Call and SMS a dialable phone.
struct ContactPills: View {
    @Environment(\.openURL) private var openURL
    @State private var showsMapChoice = false
    let address: String?
    let coordinate: Coordinate?
    let phone: String?
    var navigates = true

    private var items: [Contact] {
        var items: [Contact] = []
        if navigates {
            items.append(Contact(label: L10n.Orders.actionNavigate, icon: "map") { showsMapChoice = true })
        }
        if let call = ContactActions.callURL(phone: phone) {
            items.append(Contact(label: L10n.Orders.actionCall, icon: "phone") { openURL(call) })
        }
        if let sms = ContactActions.smsURL(phone: phone) {
            items.append(Contact(label: L10n.Orders.actionSms, icon: "message") { openURL(sms) })
        }
        return items
    }

    private struct Contact {
        let label: String
        let icon: String
        let action: () -> Void
    }

    var body: some View {
        let items = items
        if !items.isEmpty {
            ContactRows(spacing: Spacing.xs) {
                ForEach(items.indices, id: \.self) { chip(items[$0]) }
            }
            .navigateChoice(isPresented: $showsMapChoice, address: address, coordinate: coordinate)
        }
    }

    private func chip(_ item: Contact) -> some View {
        TonalChip(title: item.label, icon: item.icon, fills: true, action: item.action)
            .accessibilityLabel(item.label)
    }
}

extension View {
    /// Navigate's choice of maps app, for the address and its coordinate.
    func navigateChoice(isPresented: Binding<Bool>, address: String?, coordinate: Coordinate?) -> some View {
        modifier(NavigateChoice(isPresented: isPresented, address: address, coordinate: coordinate))
    }
}

private struct NavigateChoice: ViewModifier {
    @Environment(\.openURL) private var openURL
    @Binding var isPresented: Bool
    let address: String?
    let coordinate: Coordinate?

    func body(content: Content) -> some View {
        content.confirmationDialog(L10n.Orders.actionNavigate, isPresented: $isPresented, titleVisibility: .visible) {
            if let url = ContactActions.mapsURL(coordinate: coordinate, address: address) {
                Button(L10n.Orders.appleMaps) { openURL(url) }
            }
            if let url = ContactActions.googleMapsURL(coordinate: coordinate, address: address) {
                Button(L10n.Orders.googleMaps) { openURL(url) }
            }
            Button(L10n.cancel, role: .cancel) {}
        }
    }
}

/// The contacts in equal shares: one row while the widest chip fits its share, else the first alone over the rest,
/// else one column. As the design's own fit does, a share may fall half a point short of a chip's need.
struct ContactRows: Layout {
    var spacing: CGFloat

    /// How many chips each row holds, for chips needing `needs` across `width`.
    static func rows(needs: [CGFloat], width: CGFloat, spacing: CGFloat) -> [Int] {
        func fits(_ row: ArraySlice<CGFloat>) -> Bool {
            let share = (width - spacing * CGFloat(row.count - 1)) / CGFloat(row.count)
            return (row.max() ?? 0) <= share + 0.5
        }
        if needs.count > 1, fits(needs[...]) { return [needs.count] }
        if needs.count == 3, fits(needs.prefix(1)), fits(needs.suffix(2)) { return [1, 2] }
        return needs.map { _ in 1 }
    }

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let needs = subviews.map { $0.sizeThatFits(.unspecified).width }
        let width = proposal.width
            ?? (needs.max() ?? 0) * CGFloat(needs.count) + spacing * CGFloat(max(needs.count - 1, 0))
        return CGSize(width: width, height: frames(subviews, width: width).map(\.maxY).max() ?? 0)
    }

    func placeSubviews(in bounds: CGRect, proposal _: ProposedViewSize, subviews: Subviews, cache _: inout ()) {
        for (index, frame) in frames(subviews, width: bounds.width).enumerated() {
            subviews[index].place(
                at: CGPoint(x: bounds.minX + frame.minX, y: bounds.minY + frame.minY),
                proposal: ProposedViewSize(frame.size)
            )
        }
    }

    private func frames(_ subviews: Subviews, width: CGFloat) -> [CGRect] {
        let needs = subviews.map { $0.sizeThatFits(.unspecified).width }
        var frames: [CGRect] = []
        var top: CGFloat = 0
        for count in Self.rows(needs: needs, width: width, spacing: spacing) {
            let share = (width - spacing * CGFloat(count - 1)) / CGFloat(count)
            let first = frames.count
            let height = (first ..< first + count)
                .map { subviews[$0].sizeThatFits(ProposedViewSize(width: share, height: nil)).height }.max() ?? 0
            for position in 0 ..< count {
                frames.append(CGRect(x: CGFloat(position) * (share + spacing), y: top, width: share, height: height))
            }
            top += height + spacing
        }
        return frames
    }
}

/// The customer's name beside the phone, on the step that needs them and on a finished job.
struct CustomerLine: View {
    let order: OrderDetail
    var icon = true

    var body: some View {
        let name = order.customerName.flatMap { $0.isBlank ? nil : $0 }
        let phone = order.customerPhone.flatMap { $0.isBlank ? nil : $0 }
        if name != nil || phone != nil {
            HStack(alignment: .firstTextBaseline, spacing: 10) {
                if icon {
                    Image(systemName: "person")
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .capBandAligned(of: CleansiaTypography.scale.bodyLarge)
                        .accessibilityHidden(true)
                }
                if let name {
                    Text(name)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer(minLength: Spacing.xs)
                if let phone {
                    Text(phone)
                        .font(CleansiaTypography.bodyMedium)
                        .monospacedDigit()
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize()
                }
            }
            .accessibilityElement(children: .combine)
        }
    }
}

/// How to get in: the mode beside a door, floor and flat, then the customer's own words in a well with Copy. Each
/// piece is left out when the server sent none, with its separator; with no words, no well and no Copy.
struct AccessBlock: View {
    let order: OrderDetail
    var showsText = true

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if let mode = L10n.Orders.accessMode(order.accessMode) {
                HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                    AccessDoor()
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .capBandAligned(of: CleansiaTypography.scale.bodyLarge)
                        .accessibilityHidden(true)
                    Text(mode)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            if !order.floorAndFlat.isEmpty {
                OrdersDotFlow(
                    items: order.floorAndFlat.map { Text($0) },
                    style: CleansiaTypography.scale.bodyMedium
                )
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            if showsText, order.showsAccessCard, let access = order.accessInstructions {
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    CustomerText(text: access)
                    CopyInstructionButton(text: access).padding(.leading, -Spacing.xxs)
                }
                .padding(.horizontal, Spacing.m)
                .padding(.top, 14)
                .padding(.bottom, Spacing.hair)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(CleansiaColors.surfaceVariant, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
                .padding(.top, Spacing.hair)
            }
        }
    }
}

/// The access mode's door, outlined: SF Symbols draws its only closed door filled. 18 at the default text size, grown
/// with the type.
private struct AccessDoor: View {
    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        let traits = UITraitCollection(preferredContentSizeCategory: UIContentSizeCategory(typeSize))
        let side = UIFontMetrics.default.scaledValue(for: 18, compatibleWith: traits)
        DoorOutline()
            .stroke(style: StrokeStyle(lineWidth: side * 1.8 / 24, lineCap: .round, lineJoin: .round))
            .frame(width: side, height: side)
    }
}

/// A door on a 24 grid: the leaf with rounded top corners, the floor line and the knob.
private struct DoorOutline: Shape {
    func path(in rect: CGRect) -> Path {
        let unit = rect.width / 24
        let grid = { (column: CGFloat, row: CGFloat) in
            CGPoint(x: rect.minX + column * unit, y: rect.minY + row * unit)
        }
        var path = Path()
        path.move(to: grid(6, 20.5))
        path.addArc(tangent1End: grid(6, 3.5), tangent2End: grid(18, 3.5), radius: unit)
        path.addArc(tangent1End: grid(18, 3.5), tangent2End: grid(18, 20.5), radius: unit)
        path.addLine(to: grid(18, 20.5))
        path.move(to: grid(4, 20.5))
        path.addLine(to: grid(20, 20.5))
        path.addEllipse(in: CGRect(origin: grid(13.5, 11.5), size: CGSize(width: 2 * unit, height: 2 * unit)))
        return path
    }
}

/// What the customer wrote, as written: read in its own language, so VoiceOver does not read Czech with an
/// English voice.
struct CustomerText: View {
    let text: String

    var body: some View {
        Text(Self.spoken(text))
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurface)
            .fixedSize(horizontal: false, vertical: true)
            .textSelection(.enabled)
    }

    static func spoken(_ text: String) -> AttributedString {
        var spoken = AttributedString(L10n.tie(text))
        spoken.languageIdentifier = "cs"
        return spoken
    }
}

/// What the customer wrote: the notes and the special instructions, each under its label.
struct FromCustomerNotes: View {
    let order: OrderDetail

    static func shows(_ order: OrderDetail) -> Bool {
        !(order.customerNotes ?? "").isBlank || !(order.specialInstructions ?? "").isBlank
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            note(L10n.Orders.noteGeneralLabel, order.customerNotes)
            note(L10n.Orders.noteSpecialLabel, order.specialInstructions)
        }
    }

    @ViewBuilder
    private func note(_ label: String, _ body: String?) -> some View {
        if let body, !body.isBlank {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(label)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                CustomerText(text: body)
            }
        }
    }
}

/// A part's eyebrow inside a step or a section: "Cleaning checklist", "Photos", "How to get in".
struct DetailEyebrow: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.labelMedium)
            .foregroundColor(CleansiaColors.primaryText)
            .fixedSize(horizontal: false, vertical: true)
            .accessibilityAddTraits(.isHeader)
    }
}

/// *Property & scope*: what the job is, as labelled rows, then the facts the board flags. On her own job the rows are
/// left out while the checklist says the scope; on a job not taken it carries the board's cash and crew flags too.
struct ScopeSection: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    var rows = true
    var board = false
    var lateJoin = false

    var body: some View {
        DetailSection(title: L10n.Orders.scopeSectionTitle, icon: "house") {
            VStack(alignment: .leading, spacing: 10) {
                if rows {
                    VStack(alignment: .leading, spacing: 0) {
                        if let service {
                            row(L10n.Orders.scopeServicesLabel, service)
                        }
                        if !property.isEmpty {
                            row(L10n.Orders.propertyLabel, property)
                        }
                        if let extras {
                            row(L10n.Orders.scopeExtrasLabel, extras)
                        }
                    }
                }
                let flags = flags
                if !flags.isEmpty {
                    ChipFlow(spacing: 6, wrapsWideItems: true) {
                        ForEach(flags.indices, id: \.self) { flags[$0] }
                    }
                }
                if lateJoin, order.crew.map({ $0.crewSize > 1 }) == true {
                    OrdersCaption(text: L10n.Orders.samePayLate)
                }
            }
        }
    }

    private func row(_ label: String, _ value: String) -> some View {
        OrderInfoRow(label: label, value: value).padding(.vertical, 3)
    }

    private var service: String? {
        order.packages.first.map { $0.localizedName(for: locale) }
            ?? order.services.first.map { $0.localizedName(for: locale) }
    }

    private var property: String {
        [
            order.rooms > 0 ? L10n.Orders.rooms(order.rooms) : nil,
            order.bathrooms > 0 ? L10n.Orders.baths(order.bathrooms) : nil
        ].compactMap { $0 }.joined(separator: OrdersBoardRules.dot)
    }

    private var extras: String? {
        let names = OrdersBoardRules.orderedExtraSlugs(order.extras).map(OrdersBoardRules.shortExtraName)
        guard let first = names.first else { return nil }
        return ([first.prefix(1).uppercased(with: locale) + first.dropFirst()] + names.dropFirst())
            .joined(separator: ", ")
    }

    private var flags: [TintedFlag] {
        var flags: [TintedFlag] = []
        if order.dirtinessLevel == ._1 || order.dirtinessLevel == ._2,
           let dirt = L10n.Orders.dirtinessLevel(order.dirtinessLevel)
        {
            flags.append(TintedFlag(icon: "sparkles", text: dirt))
        }
        if board, let cash = order.cashLine {
            flags.append(TintedFlag(icon: "banknote", text: cash, tone: .amber))
        }
        if let crew = order.crew, crew.crewSize > 1 {
            let words = board
                ? OrdersBoardRules.teamWords(required: crew.crewSize, taken: crew.taken)
                : L10n.Orders.heroTeam(crew.crewSize)
            if let words { flags.append(TintedFlag(icon: "person.2", text: words)) }
        }
        return flags
    }
}

/// Copy for the two blocks a cleaner has to retype at a door — the customer's instructions and the
/// access note: the app's one copy gesture, a text link with its glyph.
struct CopyInstructionButton: View {
    @Environment(\.snackbarController) private var snackbar
    let text: String

    var body: some View {
        CleansiaTextLink(L10n.Orders.copyInstruction, leadingIcon: "doc.on.doc") {
            Self.copy(text)
            snackbar.showSuccess(L10n.Orders.copiedInstruction)
            OrdersAccessibility.announce(L10n.Orders.copiedInstruction)
        }
        .accessibilityLabel(L10n.Orders.copyInstruction)
    }

    static func copy(
        _ text: String,
        now: Date = Date(),
        write: ([[String: Any]], [UIPasteboard.OptionsKey: Any]) -> Void = UIPasteboard.general.setItems
    ) {
        write([[UTType.utf8PlainText.identifier: text]], pasteboardOptions(now: now))
    }

    /// A door code stays on this phone, never handed to Universal Clipboard, and is gone after five minutes.
    private static func pasteboardOptions(now: Date) -> [UIPasteboard.OptionsKey: Any] {
        [.localOnly: true, .expirationDate: now.addingTimeInterval(5 * 60)]
    }
}

/// Finishing a cash job: what the customer wrote about paying, then the three steps left. The amount is the head's
/// flag and the pinned move's; how it is settled, the Payment section's.
struct CashBlock: View {
    let order: OrderDetail
    let afterPhoto: Date?

    var body: some View {
        if let special = order.specialInstructions, !special.isBlank {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(verbatim: "\(L10n.Orders.fromCustomerSectionTitle)\(OrdersBoardRules.dot)"
                    + L10n.Orders.noteSpecialLabel)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                CustomerText(text: special)
            }
        }
        VStack(alignment: .leading, spacing: 0) {
            GateRow(
                title: L10n.Orders.finishAfterPhoto,
                done: afterPhoto != nil || order.hasAfterPhotos,
                stamp: afterPhoto
            )
            GateRow(title: L10n.Orders.finishCashCollected, done: order.payment.isSettled)
            GateRow(title: L10n.Orders.completeJob, done: false)
        }
    }
}

/// A line of quiet words: a caption, a hint, the details closing.
struct OrdersCaption: View {
    let text: String

    var body: some View {
        Text(L10n.tie(text))
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .fixedSize(horizontal: false, vertical: true)
    }
}

extension OrderDetail {
    /// "Cash: collect 1 240 Kč for the company", "Cash: collected …" once it is paid: the head's flag and the board's.
    var cashLine: String? {
        guard payment.isCash, let cash = cashDueLabel else { return nil }
        return L10n.Orders.cashFlag(OrdersBoardRules.nonBreaking(cash), collected: payment.isSettled)
    }

    /// The head's cash flag, on her job until it is complete.
    var cashFlag: String? {
        [._2, ._3, ._4].contains(status) ? cashLine : nil
    }
}

extension [OrderListItem] {
    func onDay(_ day: Date?) -> [OrderListItem] {
        guard let day else { return [] }
        return filter { $0.cleaningDateTime.map { Calendar.current.isDate($0, inSameDayAs: day) } ?? false }
    }

    /// Still ahead of `now` (or under way), soonest first, without `id`.
    func upcoming(now: Date, except id: String?) -> [OrderListItem] {
        filter { $0.id != id && (OrdersBoardRules.bookedEnd($0) ?? .distantPast) > now }
            .sorted { ($0.cleaningDateTime ?? .distantFuture) < ($1.cleaningDateTime ?? .distantFuture) }
    }
}
