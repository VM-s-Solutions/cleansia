import CleansiaCore
import CoreLocation
import SwiftUI

/// The detail's page: the job's map behind one surface panel, still before the take and hers to pan and zoom after, the
/// back button fixed over the map, the state's figure on the panel's top edge and the move pinned under the thumb. The
/// panel rests expanded at the band's foot or, dragged down, collapsed onto its head, which stays put while the rest
/// scrolls inside the panel.
struct JobDetailFrame<Head: View, Sheet: View, Pinned: View>: View {
    /// The figure stands on the sheet's edge: 128, 104 on a short screen, centred on the edge, or with its ledge on it.
    static func clearance(for art: MascotArt?, shortScreen: Bool) -> MascotClearance {
        guard let art else { return .none }
        let side: CGFloat = shortScreen ? 104 : 128
        let lift = MascotPuck.ledgeFraction(art).map { side * $0 } ?? side / 2
        return MascotClearance(width: side - Spacing.xs, foot: side - lift - Spacing.m)
    }

    /// Short is an SE's window, read whole: the detail hides both bars, so the room inside them says nothing.
    static func isShortScreen(windowHeight: CGFloat) -> Bool {
        windowHeight < 700
    }

    static var stops: Set<SnapAnchor> {
        [.mapFocus, .expanded]
    }

    /// The band's height under the status bar: the back button and the figure's upper half, 40 more over a map.
    static func bandHeight(figure: Bool, map: Bool) -> CGFloat {
        (figure ? 96 : 66) + Spacing.l + (map ? Spacing.xxl : 0)
    }

    /// The expanded panel's top under the status bar: the band, less the 24 the panel laps over it.
    static func expandedTop(figure: Bool, map: Bool) -> CGFloat {
        bandHeight(figure: figure, map: map) - Spacing.l
    }

    /// The collapsed panel's height, ending clean under the head: the handle (8 over its 5, 4 under), the head, and
    /// the 8 its edge takes once the rest has scrolled, which the rest's first gap leaves empty at rest. With no head
    /// of its own, the sheet's own share.
    static func peekHeight(head: CGFloat) -> CGFloat {
        head > 0 ? Spacing.xs + 5 + Spacing.xxs + head + Spacing.xs : 0
    }

    @Environment(\.dismiss) private var dismiss
    @Environment(\.dynamicTypeSize) private var typeSize
    @Environment(\.jobPanelOpensCollapsed) private var opensCollapsed
    private let mascot: MascotArt?
    private let endsWithMove: Bool
    private let map: JobBandMap?
    private let head: () -> Head
    private let sheet: () -> Sheet
    private let pinned: () -> Pinned

    @State private var anchor = SnapAnchor.expanded
    @State private var headHeight: CGFloat = 0
    @State private var shortScreen = false
    @State private var mapMovedAway = false
    @State private var scrolled = false

    init(
        mascot: MascotArt?,
        endsWithMove: Bool = true,
        map: JobBandMap? = nil,
        @ViewBuilder head: @escaping () -> Head,
        @ViewBuilder sheet: @escaping () -> Sheet,
        @ViewBuilder pinned: @escaping () -> Pinned
    ) {
        self.mascot = mascot
        self.endsWithMove = endsWithMove
        self.map = map
        self.head = head
        self.sheet = sheet
        self.pinned = pinned
    }

    private var figure: MascotArt? {
        typeSize.isAccessibilitySize ? nil : mascot
    }

    private var side: CGFloat {
        shortScreen ? 104 : 128
    }

    /// At accessibility sizes a head can outgrow the panel, so it scrolls with the rest.
    private var pinsHead: Bool {
        Head.self != EmptyView.self && !typeSize.isAccessibilitySize
    }

    var body: some View {
        let peek = pinsHead ? Self.peekHeight(head: headHeight) : 0
        VStack(spacing: 0) {
            SnapSheet(
                anchor: $anchor,
                ornamentSize: side,
                minimumSheetHeight: peek,
                stops: Self.stops,
                minimumSheetTop: Self.expandedTop(figure: figure != nil, map: map != nil),
                mapFocusHeight: peek > 0 ? peek : nil
            ) {
                backdrop
            } ornament: {
                puck
            } content: {
                panel
            }
            .overlay(alignment: .topLeading) {
                HStack(spacing: Spacing.xs) {
                    FloatingCircleButton(systemIcon: "chevron.left", accessibilityLabel: L10n.Orders.title) {
                        dismiss()
                    }
                    if map?.showsRecentre(movedAway: mapMovedAway) == true {
                        FloatingCircleButton(systemIcon: "scope", accessibilityLabel: L10n.Orders.mapRecentre) {
                            mapMovedAway = false
                        }
                        .transition(.opacity)
                    }
                }
                .animation(.easeOut(duration: 0.2), value: mapMovedAway)
                .padding(.leading, Spacing.m)
                .padding(.top, Spacing.xxs + Spacing.hair)
            }
            pinned()
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .background(GeometryReader { space in
            let window = space.size.height + space.safeAreaInsets.top + space.safeAreaInsets.bottom
            Color.clear.onAppear { shortScreen = Self.isShortScreen(windowHeight: window) }
        })
        .onAppear { if opensCollapsed { anchor = .mapFocus } }
    }

    /// The map, or the band's colour with none; it never moves with the panel.
    private var backdrop: some View {
        ZStack {
            CleansiaColors.primaryContainer
            if let map {
                JobBandMapView(
                    map: map,
                    visible: Self.expandedTop(figure: figure != nil, map: true),
                    figure: figure == nil ? 0 : side + Spacing.m,
                    movedAway: $mapMovedAway
                )
            }
        }
    }

    private var panel: some View {
        VStack(alignment: .leading, spacing: 0) {
            if pinsHead {
                head()
                    .padding(.horizontal, Spacing.m)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(GeometryReader { space in
                        Color.clear
                            .onAppear { headHeight = space.size.height }
                            .onChange(of: space.size.height) { headHeight = $0 }
                    })
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    if !pinsHead {
                        head()
                    }
                    sheet()
                }
                .padding(.horizontal, Spacing.m)
                .padding(.bottom, endsWithMove ? Spacing.m : Spacing.xxl)
                .frame(maxWidth: .infinity, alignment: .topLeading)
                .background(GeometryReader { content in
                    Color.clear.preference(
                        key: OrdersScrolledKey.self,
                        value: content.frame(in: .named(OrdersScrolledKey.space)).minY < -0.5
                    )
                })
            }
            .coordinateSpace(name: OrdersScrolledKey.space)
            .onPreferenceChange(OrdersScrolledKey.self) { scrolled = $0 }
            .overlay(alignment: .top) {
                if pinsHead {
                    headEdge
                }
            }
        }
        .environment(\.mascotClearance, Self.clearance(for: figure, shortScreen: shortScreen))
    }

    /// Once the rest has scrolled, the head reaches 8 further on the panel's surface and ends in the footer's rule, so
    /// a line going under it is cut at a rule, never left as a sliver under the head's last line.
    private var headEdge: some View {
        CleansiaColors.surface
            .frame(height: Spacing.xs)
            .overlay(alignment: .bottom) { Divider().overlay(CleansiaColors.outlineVariant) }
            .opacity(scrolled ? 1 : 0)
            .allowsHitTesting(false)
    }

    /// Its ledge, or its centre, on the panel's edge.
    @ViewBuilder
    private var puck: some View {
        if let figure {
            let lift = MascotPuck.ledgeFraction(figure).map { side * $0 } ?? side / 2
            MascotPuck(figure)
                .frame(width: side, height: side)
                .offset(y: side / 2 - lift)
        }
    }
}

extension JobDetailFrame where Head == EmptyView {
    init(mascot: MascotArt?, @ViewBuilder sheet: @escaping () -> Sheet, @ViewBuilder pinned: @escaping () -> Pinned) {
        self.init(mascot: mascot, head: { EmptyView() }, sheet: sheet, pinned: pinned)
    }
}

extension JobDetailFrame where Head == EmptyView, Pinned == EmptyView {
    init(mascot: MascotArt?, @ViewBuilder sheet: @escaping () -> Sheet) {
        self.init(mascot: mascot, endsWithMove: false, head: { EmptyView() }, sheet: sheet, pinned: { EmptyView() })
    }
}

private struct JobPanelOpensCollapsedKey: EnvironmentKey {
    static let defaultValue = false
}

extension EnvironmentValues {
    /// The detail's panel opens collapsed onto its head, as if dragged down.
    var jobPanelOpensCollapsed: Bool {
        get { self[JobPanelOpensCollapsedKey.self] }
        set { self[JobPanelOpensCollapsedKey.self] = newValue }
    }
}

/// What the detail's band draws: the job's area, blurred and dimmed, until she takes it, with the chip that says when
/// the address comes; her job's address pinned, the street over the pin. Her job with no coordinate keeps the area map
/// without the chip.
enum JobBandMap: Equatable {
    case area(String?, locked: Bool)
    case pin(Coordinate, street: String?)

    /// Her job's pinned map pans and zooms under her finger; the area map stays still.
    var moves: Bool {
        if case .pin = self { return true }
        return false
    }

    func showsRecentre(movedAway: Bool) -> Bool {
        moves && movedAway
    }

    /// The market's main city: where an area that cannot be found is drawn.
    static let marketCity = Coordinate(latitude: 50.0755, longitude: 14.4378)

    /// A job she has not taken is drawn from its area alone: no coordinate of it is ever read.
    static func mode(location: OrderLocation, area: String?, taken: Bool) -> JobBandMap {
        guard taken else {
            if case let .approximate(zone) = location { return .area(area ?? zone, locked: true) }
            return .area(area, locked: true)
        }
        if let target = location.navigationTarget, let coordinate = target.coordinate {
            return .pin(coordinate, street: OrdersBoardRules.streetLine(target.line))
        }
        return .area(area ?? location.line, locked: false)
    }
}

/// Finds an area's centre on the device, once per area; an area it cannot find, or none, is the market's city.
@MainActor
final class JobAreaLocator: ObservableObject {
    static var marketCity: Coordinate {
        JobBandMap.marketCity
    }

    @Published private(set) var centre: Coordinate?
    private let geocode: (String) async -> Coordinate?
    private var known: [String: Coordinate?] = [:]

    init(geocode: @escaping (String) async -> Coordinate? = JobAreaLocator.lookUp) {
        self.geocode = geocode
    }

    func locate(_ area: String?) async {
        guard let area, !area.isBlank else {
            centre = Self.marketCity
            return
        }
        if let known = known[area] {
            centre = known ?? Self.marketCity
            return
        }
        let found = await geocode(area)
        known[area] = .some(found)
        centre = found ?? Self.marketCity
    }

    /// The area's name, looked up near the market's city.
    nonisolated static func lookUp(_ area: String) async -> Coordinate? {
        let city = JobBandMap.marketCity
        let near = CLCircularRegion(
            center: CLLocationCoordinate2D(latitude: city.latitude, longitude: city.longitude),
            radius: 300_000,
            identifier: "market"
        )
        guard let place = try? await CLGeocoder().geocodeAddressString(area, in: near, preferredLocale: nil).first,
              let location = place.location
        else { return nil }
        return Coordinate(latitude: location.coordinate.latitude, longitude: location.coordinate.longitude)
    }
}

/// The band's map framed behind the panel at the backdrop's whole `size`: `top` of it under the status bar and
/// `visible` of it above the expanded panel, at the band's metres to the point, with the pin and the lock chip where
/// the band put them.
struct JobBandMapLayout: Equatable {
    let size: CGSize
    let top: CGFloat
    let visible: CGFloat

    /// A square as tall as the map, centred across it: MapKit fits a square region to its height at any width.
    var side: CGFloat {
        max(size.width, size.height)
    }

    private var overhang: CGFloat {
        (side - size.width) / 2
    }

    /// The band's map stood `top + visible + 24` tall: the same metres to the point over the whole height.
    func metres(_ band: Double) -> Double {
        let bandHeight = top + visible + Spacing.l
        return bandHeight > 0 ? band * Double(size.height / bandHeight) : band
    }

    /// The pin's tip at 72 % of the visible band.
    var pinAnchor: CGFloat {
        size.height > 0 ? (top + visible * 0.72) / size.height : 0.5
    }

    var chipY: CGFloat {
        top + visible * 0.36
    }

    /// The logo and the legal link 8 above the panel's edge, inside the phone's width and short of the figure.
    func attributionInsets(sheetTop: CGFloat, figure: CGFloat) -> EdgeInsets {
        EdgeInsets(
            top: 0,
            leading: overhang + Spacing.s,
            bottom: (sheetTop > 0 ? max(size.height - sheetTop, 0) : 0) + Spacing.xs,
            trailing: overhang + Spacing.s + figure
        )
    }
}

/// The map behind the panel, laid out by `JobBandMapLayout`. The area map is blurred, dimmed and still under its sharp
/// logo and legal link, the lock chip over it until the job is hers; her pinned map pans and zooms, the pin and its
/// street on the address, and says in `movedAway` when it no longer shows the job as framed. The logo and the legal
/// link ride the panel's edge.
struct JobBandMapView: View {
    @Environment(\.snapSheetTop) private var sheetTop
    @Environment(\.snapSheetSafeTop) private var safeTop
    let map: JobBandMap
    let visible: CGFloat
    var figure: CGFloat = 0
    @Binding var movedAway: Bool
    @StateObject private var locator = JobAreaLocator()

    var body: some View {
        GeometryReader { space in
            let layout = JobBandMapLayout(size: space.size, top: safeTop, visible: visible)
            canvas(layout)
                .frame(width: layout.side, height: space.size.height)
                .frame(width: space.size.width, height: space.size.height)
                .clipped()
                .overlay {
                    if case .area(_, locked: true) = map {
                        lockChip
                            .frame(maxWidth: max(space.size.width - 136, 0))
                            .fixedSize(horizontal: false, vertical: true)
                            .position(x: space.size.width / 2, y: layout.chipY)
                    }
                }
        }
    }

    @ViewBuilder
    private func canvas(_ layout: JobBandMapLayout) -> some View {
        let insets = layout.attributionInsets(sheetTop: sheetTop, figure: figure)
        switch map {
        case let .pin(coordinate, street):
            PlaceMap(
                coordinate: coordinate,
                metres: layout.metres(700),
                anchor: layout.pinAnchor,
                label: street,
                attributionInsets: insets,
                movedAway: $movedAway
            )
        case let .area(area, _):
            ZStack {
                if let centre = locator.centre {
                    PlaceMap(
                        coordinate: centre,
                        metres: layout.metres(6000),
                        pinned: false,
                        veiled: true,
                        attributionInsets: insets
                    )
                } else {
                    Color.black.opacity(0.25)
                }
            }
            .accessibilityHidden(true)
            .task(id: area) { await locator.locate(area) }
        }
    }

    private var lockChip: some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Image(systemName: "lock.fill")
                .font(CleansiaTypography.labelMedium)
                .capBandAligned(of: CleansiaTypography.scale.labelMedium)
                .accessibilityHidden(true)
            Text(L10n.Orders.exactAddressAfterTake)
                .font(CleansiaTypography.labelMedium)
                .multilineTextAlignment(.center)
                .fixedSize(horizontal: false, vertical: true)
        }
        .foregroundColor(CleansiaColors.onSurface)
        .padding(.horizontal, 14)
        .padding(.vertical, Spacing.xs)
        .background(CleansiaColors.surface.opacity(0.88), in: Capsule())
        .overlay(Capsule().strokeBorder(CleansiaColors.outlineVariant))
        .dynamicTypeSize(...DynamicTypeSize.xxLarge)
        .accessibilityElement(children: .combine)
    }
}

/// A section of the sheet: its icon header over a rule, then its rows 12 under it, 24 under what came before.
struct DetailSection<Content: View>: View {
    let title: String
    let icon: String
    var count: String?
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            OrderSectionHeaderRow(title: title, systemImage: icon, subtitle: count)
                .accessibilityElement(children: .ignore)
                .accessibilityLabel([title, count].compactMap { $0 }.joined(separator: ", "))
                .accessibilityAddTraits(.isHeader)
            content()
        }
        .padding(.top, Spacing.l)
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}
