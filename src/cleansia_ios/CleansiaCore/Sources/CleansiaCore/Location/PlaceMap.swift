import MapKit
import SwiftUI

/// A map of one place in the one map look, `metres` tall, the place at `anchor` of the map's height and centred across
/// it. It stands still unless it is handed `movedAway`: then it pans and pinch-zooms, never turns or tilts, writes
/// whether it has been moved off the place as framed, and frames the place again, animated, once that is set back to
/// false. A pinned map carries the Cleansia pin on the place and, over the pin, a short label on a chip that travels
/// with it. A veiled map is blurred and dimmed under its logo and legal link, which stay sharp; `attributionInsets`
/// moves those two clear of whatever covers the map's edge, and a change to it moves nothing else. Decorative to
/// VoiceOver: whatever it shows is written beside it.
public struct PlaceMap: View {
    private let coordinate: Coordinate
    private let metres: CLLocationDistance
    private let anchor: CGFloat
    private let pinned: Bool
    private let label: String?
    private let veiled: Bool
    private let attributionInsets: EdgeInsets?
    private let movedAway: Binding<Bool>?

    public init(
        coordinate: Coordinate,
        metres: CLLocationDistance = 700,
        anchor: CGFloat = 0.5,
        pinned: Bool = true,
        label: String? = nil,
        veiled: Bool = false,
        attributionInsets: EdgeInsets? = nil,
        movedAway: Binding<Bool>? = nil
    ) {
        self.coordinate = coordinate
        self.metres = metres
        self.anchor = anchor
        self.pinned = pinned
        self.label = label
        self.veiled = veiled
        self.attributionInsets = attributionInsets
        self.movedAway = movedAway
    }

    public var body: some View {
        PlaceMapView(
            coordinate: coordinate,
            metres: metres,
            anchor: anchor,
            pinned: pinned,
            label: pinned && label?.isEmpty == false ? label : nil,
            veiled: veiled,
            attributionInsets: attributionInsets,
            movedAway: movedAway
        )
        .accessibilityHidden(true)
    }

    /// From the pin's tip to the label's centre: the pin, a 4 gap and half the chip.
    static var pinLift: CGFloat {
        CleansiaMapMarker.pinSize.height + 4 + 11
    }

    /// The region that puts `coordinate` at `anchor` of the map's height: `metres` tall, its centre moved off the
    /// place by the anchor's distance from the middle. A wide map fits its height to the region, so the share holds.
    static func region(for coordinate: Coordinate, metres: CLLocationDistance, anchor: CGFloat) -> MKCoordinateRegion {
        let place = CLLocationCoordinate2D(latitude: coordinate.latitude, longitude: coordinate.longitude)
        let span = MKCoordinateRegion(center: place, latitudinalMeters: metres, longitudinalMeters: metres).span
        let centre = CLLocationCoordinate2D(
            latitude: coordinate.latitude + (Double(anchor) - 0.5) * span.latitudeDelta,
            longitude: coordinate.longitude
        )
        return MKCoordinateRegion(center: centre, span: span)
    }
}

private struct PlaceMapLabel: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.labelMedium)
            .foregroundColor(CleansiaColors.onSurface)
            .lineLimit(1)
            .truncationMode(.tail)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, 3)
            .background(CleansiaColors.surface.opacity(0.92), in: RoundedRectangle(cornerRadius: 8))
            .dynamicTypeSize(...DynamicTypeSize.xLarge)
            .ignoresSafeArea()
    }
}

/// The street over the pin, carried by the pin's own view so it stays on the place wherever the map is moved.
final class PlaceMapStreetLabel: UIView {
    private let host = UIHostingController(rootView: PlaceMapLabel(text: ""))
    private var text = ""
    private var maxWidth: CGFloat = 240

    override init(frame: CGRect) {
        super.init(frame: frame)
        host.view.backgroundColor = .clear
        addSubview(host.view)
        isUserInteractionEnabled = false
        accessibilityElementsHidden = true
    }

    @available(*, unavailable)
    required init?(coder _: NSCoder) {
        fatalError("PlaceMapStreetLabel is not storyboard-instantiable")
    }

    /// `text` on a pin's view, as wide as it reads up to 240 and 32 short of either side of the map; none, no label.
    static func place(_ text: String?, on pin: MKAnnotationView, in mapView: MKMapView) {
        let shown = pin.subviews.compactMap { $0 as? PlaceMapStreetLabel }
        guard let text else {
            shown.forEach { $0.removeFromSuperview() }
            return
        }
        let label = shown.first ?? PlaceMapStreetLabel()
        if label.superview == nil { pin.addSubview(label) }
        let room = mapView.bounds.width > 0 ? mapView.bounds.width - 2 * Spacing.xl : .infinity
        label.show(text, maxWidth: min(room, 240))
    }

    private func show(_ text: String, maxWidth: CGFloat) {
        self.text = text
        self.maxWidth = maxWidth
        host.rootView = PlaceMapLabel(text: text)
        let size = host.sizeThatFits(in: CGSize(width: maxWidth, height: .greatestFiniteMagnitude))
        bounds = CGRect(origin: .zero, size: size)
        host.view.frame = bounds
        center = CGPoint(
            x: CleansiaMapMarker.pinSize.width / 2,
            y: CleansiaMapMarker.pinSize.height - PlaceMap.pinLift
        )
    }

    override func traitCollectionDidChange(_ previous: UITraitCollection?) {
        super.traitCollectionDidChange(previous)
        guard previous?.preferredContentSizeCategory != traitCollection.preferredContentSizeCategory else { return }
        show(text, maxWidth: maxWidth)
    }
}

struct PlaceMapView: UIViewRepresentable {
    let coordinate: Coordinate
    let metres: CLLocationDistance
    let anchor: CGFloat
    let pinned: Bool
    let label: String?
    let veiled: Bool
    let attributionInsets: EdgeInsets?
    let movedAway: Binding<Bool>?

    func makeCoordinator() -> Placement {
        Placement()
    }

    func makeUIView(context: Context) -> PlaceMapCanvas {
        makeMapView(placement: context.coordinator)
    }

    func updateUIView(_ mapView: PlaceMapCanvas, context: Context) {
        apply(to: mapView, placement: context.coordinator)
    }

    func makeMapView(placement: Placement) -> PlaceMapCanvas {
        let mapView = PlaceMapCanvas(veiled: veiled)
        CleansiaMapStyle.apply(to: mapView)
        mapView.isUserInteractionEnabled = movedAway != nil
        mapView.showsUserLocation = false
        mapView.accessibilityElementsHidden = true
        mapView.delegate = placement
        apply(to: mapView, placement: placement)
        return mapView
    }

    /// A map she can move is always framed on its bounds, so where it framed the place can be told from where she
    /// moved it.
    func apply(to mapView: PlaceMapCanvas, placement: Placement) {
        placement.label = label
        placement.movedAway = movedAway
        let margins = (attributionInsets ?? (movedAway == nil ? nil : EdgeInsets())).map {
            NSDirectionalEdgeInsets(top: $0.top, leading: $0.leading, bottom: $0.bottom, trailing: $0.trailing)
        }
        let key = PlaceMapKey(coordinate: coordinate, metres: metres, anchor: anchor, pinned: pinned, label: label)
        guard placement.key != key else {
            if let margins { mapView.keepMarks(inside: margins) }
            placement.hear(on: mapView)
            return
        }
        placement.key = key
        let region = PlaceMap.region(for: coordinate, metres: metres, anchor: anchor)
        let place = CLLocationCoordinate2D(latitude: coordinate.latitude, longitude: coordinate.longitude)
        if let margins {
            mapView.frame(region, margins: margins, around: place)
        } else {
            mapView.setRegion(region, animated: false)
        }
        placement.reframed()
        mapView.removeAnnotations(mapView.annotations)
        if pinned {
            let pin = MKPointAnnotation()
            pin.coordinate = place
            mapView.addAnnotation(pin)
        }
    }

    /// The map's delegate: it vends the Cleansia pin with the street on it, and tells the binding when she moves the
    /// map off the place or back onto it.
    final class Placement: NSObject, MKMapViewDelegate {
        var key: PlaceMapKey?
        var label: String?
        var movedAway: Binding<Bool>?
        private var away = false
        private var heard = false
        /// A write still on its way to the binding: until it lands, the binding still holds the old value.
        private var pending: Bool?

        func mapView(_ mapView: MKMapView, viewFor annotation: MKAnnotation) -> MKAnnotationView? {
            let pin = CleansiaMapMarker.delegate.mapView(mapView, viewFor: annotation)
            if let pin { PlaceMapStreetLabel.place(label, on: pin, in: mapView) }
            return pin
        }

        func mapViewDidChangeVisibleRegion(_ mapView: MKMapView) {
            report(mapView)
        }

        func mapView(_ mapView: MKMapView, regionDidChangeAnimated _: Bool) {
            (mapView as? PlaceMapCanvas)?.isRecentring = false
            report(mapView)
        }

        /// The binding set back to false asks for the place again.
        func hear(on mapView: PlaceMapCanvas) {
            guard let movedAway else { return }
            let now = pending ?? movedAway.wrappedValue
            if heard, !now {
                away = false
                mapView.recentre()
            }
            heard = now
        }

        func reframed() {
            if away || heard { say(false, on: nil) }
        }

        private func report(_ mapView: MKMapView) {
            guard movedAway != nil, let canvas = mapView as? PlaceMapCanvas, !canvas.isRecentring else { return }
            let isAway = canvas.isAway
            if isAway != away { say(isAway, on: canvas) }
        }

        /// Written on the next turn: the map reports from inside SwiftUI's own update as often as from her finger.
        private func say(_ value: Bool, on canvas: PlaceMapCanvas?) {
            guard let movedAway else { return }
            canvas?.keepsHerView = value
            away = value
            heard = value
            pending = value
            DispatchQueue.main.async { [weak self] in
                if self?.pending == value { self?.pending = nil }
                movedAway.wrappedValue = value
            }
        }
    }
}

final class PlaceMapCanvas: MKMapView {
    /// Where the place stands on the map, and at what scale.
    struct Look: Equatable {
        let pin: CGPoint
        let metresPerPoint: Double
    }

    private let veil: UIView?
    private var target: MKCoordinateRegion?
    private var framedSize: CGSize = .zero
    private var place: CLLocationCoordinate2D?
    private var framedLook: Look?
    private var framingIsStale = false
    var isRecentring = false
    /// She has moved the map: a new size keeps what she looks at instead of framing the place again.
    var keepsHerView = false

    init(veiled: Bool) {
        veil = veiled ? Self.makeVeil() : nil
        super.init(frame: .zero)
    }

    @available(*, unavailable)
    required init?(coder _: NSCoder) {
        fatalError("PlaceMapCanvas is not storyboard-instantiable")
    }

    /// Draws `region` across the whole map while its logo and legal link keep inside `margins`.
    func frame(_ region: MKCoordinateRegion, margins: NSDirectionalEdgeInsets, around place: CLLocationCoordinate2D) {
        insetsLayoutMarginsFromSafeArea = false
        directionalLayoutMargins = margins
        target = region
        self.place = place
        keepsHerView = false
        framedSize = .zero
        setNeedsLayout()
    }

    /// Whether the place stands elsewhere, or at another scale, than where the map framed it.
    var isAway: Bool {
        guard let place, let framedLook else { return false }
        return Self.isAway(look(at: place), from: framedLook)
    }

    /// Animated, unless the map has changed size since it last framed the place.
    func recentre(animated: Bool = true) {
        guard let target, isAway else { return }
        keepsHerView = false
        guard animated, !framingIsStale else { return frameNow(target) }
        isRecentring = true
        setRegion(Self.framed(target, in: bounds.size, margins: layoutMargins), animated: true)
    }

    /// Moved further than a fingertip slips, 8, or zoomed by more than 5 %.
    static func isAway(_ look: Look, from framed: Look) -> Bool {
        hypot(look.pin.x - framed.pin.x, look.pin.y - framed.pin.y) > 8
            || abs(look.metresPerPoint / framed.metresPerPoint - 1) > 0.05
    }

    private func look(at place: CLLocationCoordinate2D) -> Look {
        let west = convert(CGPoint(x: 0, y: bounds.midY), toCoordinateFrom: self)
        let east = convert(CGPoint(x: bounds.width, y: bounds.midY), toCoordinateFrom: self)
        return Look(
            pin: convert(place, toPointTo: self),
            metresPerPoint: MKMapPoint(west).distance(to: MKMapPoint(east)) / Double(max(bounds.width, 1))
        )
    }

    /// Moves the logo and the legal link alone: MapKit keeps its drawing still when only the margins change.
    func keepMarks(inside margins: NSDirectionalEdgeInsets) {
        guard directionalLayoutMargins != margins else { return }
        directionalLayoutMargins = margins
    }

    /// The veil sits right above the map's own drawing, under the logo and the legal link MapKit lays over it, so
    /// those two are never blurred.
    override func layoutSubviews() {
        super.layoutSubviews()
        if let target, bounds.width > 0, bounds.height > 0, bounds.size != framedSize {
            framedSize = bounds.size
            if keepsHerView {
                framingIsStale = true
            } else {
                frameNow(target)
            }
        }
        guard let veil, let drawing = subviews.first(where: { $0 !== veil }) else { return }
        veil.frame = bounds
        if subviews.firstIndex(of: veil) != subviews.firstIndex(of: drawing).map({ $0 + 1 }) {
            insertSubview(veil, aboveSubview: drawing)
        }
    }

    private func frameNow(_ target: MKCoordinateRegion) {
        framedLook = nil
        setRegion(Self.framed(target, in: bounds.size, margins: layoutMargins), animated: false)
        framedLook = place.map { look(at: $0) }
        framingIsStale = false
    }

    /// MapKit fits a region inside the layout margins, centred there. This is the region that, so fitted, draws
    /// `region` as it would fill the whole `size`: the same scale, the same point at the map's centre.
    static func framed(_ region: MKCoordinateRegion, in size: CGSize, margins: UIEdgeInsets) -> MKCoordinateRegion {
        let topLeft = MKMapPoint(CLLocationCoordinate2D(
            latitude: region.center.latitude + region.span.latitudeDelta / 2,
            longitude: region.center.longitude - region.span.longitudeDelta / 2
        ))
        let bottomRight = MKMapPoint(CLLocationCoordinate2D(
            latitude: region.center.latitude - region.span.latitudeDelta / 2,
            longitude: region.center.longitude + region.span.longitudeDelta / 2
        ))
        let width = bottomRight.x - topLeft.x
        let height = bottomRight.y - topLeft.y
        let scale = max(width / Double(size.width), height / Double(size.height))
        let innerWidth = Double(size.width - margins.left - margins.right) * scale
        let innerHeight = Double(size.height - margins.top - margins.bottom) * scale
        let midX = topLeft.x + width / 2 + Double(margins.left - margins.right) / 2 * scale
        let midY = topLeft.y + height / 2 + Double(margins.top - margins.bottom) / 2 * scale
        let top = MKMapPoint(x: midX - innerWidth / 2, y: midY - innerHeight / 2).coordinate
        let bottom = MKMapPoint(x: midX + innerWidth / 2, y: midY + innerHeight / 2).coordinate
        return MKCoordinateRegion(
            center: CLLocationCoordinate2D(
                latitude: (top.latitude + bottom.latitude) / 2,
                longitude: (top.longitude + bottom.longitude) / 2
            ),
            span: MKCoordinateSpan(
                latitudeDelta: top.latitude - bottom.latitude,
                longitudeDelta: bottom.longitude - top.longitude
            )
        )
    }

    private static func makeVeil() -> UIView {
        let blur = UIVisualEffectView(effect: UIBlurEffect(style: .systemUltraThinMaterial))
        blur.isUserInteractionEnabled = false
        let dim = UIView()
        dim.backgroundColor = UIColor.black.withAlphaComponent(0.25)
        dim.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        blur.contentView.addSubview(dim)
        return blur
    }
}

struct PlaceMapKey: Equatable {
    let coordinate: Coordinate
    let metres: CLLocationDistance
    let anchor: CGFloat
    let pinned: Bool
    let label: String?
}
