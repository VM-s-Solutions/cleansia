import MapKit
import SwiftUI

/// The Cleansia map pin — a brand-sky teardrop with a white house — on every map surface in both apps,
/// where differently drawn pins used to disagree (Android's twin is `CleansiaMapPin`, 40 × 50 dp, the
/// same tint). Decorative: the address it marks is always written beside the map. The tip is the
/// bottom-centre of its bounds, so an annotation is lifted by half its height and a centre pin by all
/// of it. MapKit's stock marker is a red balloon, so every annotation is vended through here instead.
///
/// The light/dark hexes are the source of truth and the testable surface: a SwiftUI `Color` →
/// `UIColor` roundtrip flattens dynamic providers on the iOS-16 floor.
public enum CleansiaMapMarker {
    public static let reuseIdentifier = "CleansiaMapMarker"
    public static let glyphSymbolName = "house.fill"
    public static let tint: (light: UInt32, dark: UInt32) = (light: 0x0284C7, dark: 0x38BDF8)
    public static let pinSize = CGSize(width: 40, height: 50)

    /// Stateless, so one instance serves every map — `MKMapView.delegate` is weak and a
    /// per-map instance would need an owner to outlive the assignment.
    static let delegate = MapMarkerDelegate()

    static let glyphColor = UIColor.white

    static func fillColor(dark: Bool) -> UIColor {
        UIColor(Color(hex: dark ? tint.dark : tint.light))
    }

    /// The pin for a scheme. Both drawings are registered on one image asset, so an image view holding
    /// it follows a light/dark switch by itself; the annotation view re-reads it on a trait change. The
    /// lookup names the scale the pins were drawn at — without it the asset hands back a 1x image three
    /// times the size.
    static func pinImage(for traits: UITraitCollection) -> UIImage {
        pinAsset.image(with: pinTraits(dark: traits.userInterfaceStyle == .dark))
    }

    private static let pinAsset: UIImageAsset = {
        let asset = UIImageAsset()
        asset.register(drawPin(dark: false), with: pinTraits(dark: false))
        asset.register(drawPin(dark: true), with: pinTraits(dark: true))
        return asset
    }()

    private static let pinScale = UIGraphicsImageRendererFormat.preferred().scale

    private static func pinTraits(dark: Bool) -> UITraitCollection {
        UITraitCollection(traitsFrom: [
            UITraitCollection(userInterfaceStyle: dark ? .dark : .light),
            UITraitCollection(displayScale: pinScale)
        ])
    }

    /// A circle on top whose two tangents meet in a point at the bottom-centre, white-rimmed, with
    /// the house centred on the round head.
    private static func drawPin(dark: Bool) -> UIImage {
        let format = UIGraphicsImageRendererFormat.preferred()
        format.scale = pinScale
        return UIGraphicsImageRenderer(size: pinSize, format: format).image { _ in
            let outline = teardrop(in: CGRect(origin: .zero, size: pinSize).insetBy(dx: 1, dy: 1))
            fillColor(dark: dark).setFill()
            outline.fill()
            glyphColor.setStroke()
            outline.lineWidth = 2
            outline.stroke()

            let configuration = UIImage.SymbolConfiguration(pointSize: 18, weight: .semibold)
            guard let house = UIImage(systemName: glyphSymbolName, withConfiguration: configuration)?
                .withTintColor(glyphColor, renderingMode: .alwaysOriginal)
            else { return }
            let head = CGPoint(x: pinSize.width / 2, y: pinSize.width / 2)
            house.draw(in: CGRect(
                x: head.x - house.size.width / 2,
                y: head.y - house.size.height / 2,
                width: house.size.width,
                height: house.size.height
            ))
        }
    }

    static func teardrop(in rect: CGRect) -> UIBezierPath {
        let radius = rect.width / 2
        let center = CGPoint(x: rect.midX, y: rect.minY + radius)
        let tip = CGPoint(x: rect.midX, y: rect.maxY)
        // The angle at the centre between straight down and each tangent point.
        let alpha = acos(radius / (tip.y - center.y))
        let path = UIBezierPath()
        path.move(to: tip)
        path.addArc(
            withCenter: center,
            radius: radius,
            startAngle: .pi / 2 + alpha,
            endAngle: .pi * 2 + .pi / 2 - alpha,
            clockwise: true
        )
        path.close()
        return path
    }

    /// The soft drop shadow both the annotation and the picker's centre pin carry.
    static func applyShadow(to layer: CALayer) {
        layer.shadowColor = UIColor.black.cgColor
        layer.shadowOpacity = 0.25
        layer.shadowRadius = 3
        layer.shadowOffset = CGSize(width: 0, height: 2)
    }
}

final class CleansiaPinAnnotationView: MKAnnotationView {
    override init(annotation: MKAnnotation?, reuseIdentifier: String?) {
        super.init(annotation: annotation, reuseIdentifier: reuseIdentifier)
        image = CleansiaMapMarker.pinImage(for: traitCollection)
        // The tip, not the image's centre, sits on the coordinate.
        centerOffset = CGPoint(x: 0, y: -CleansiaMapMarker.pinSize.height / 2)
        CleansiaMapMarker.applyShadow(to: layer)
        isAccessibilityElement = false
    }

    @available(*, unavailable)
    required init?(coder _: NSCoder) {
        fatalError("CleansiaPinAnnotationView is not storyboard-instantiable")
    }

    override func traitCollectionDidChange(_ previous: UITraitCollection?) {
        super.traitCollectionDidChange(previous)
        image = CleansiaMapMarker.pinImage(for: traitCollection)
    }
}

final class MapMarkerDelegate: NSObject, MKMapViewDelegate {
    func mapView(_ mapView: MKMapView, viewFor annotation: MKAnnotation) -> MKAnnotationView? {
        guard annotation is MKPointAnnotation else { return nil }
        let reused = mapView.dequeueReusableAnnotationView(withIdentifier: CleansiaMapMarker.reuseIdentifier)
        guard let pin = reused as? CleansiaPinAnnotationView else {
            return CleansiaPinAnnotationView(
                annotation: annotation,
                reuseIdentifier: CleansiaMapMarker.reuseIdentifier
            )
        }
        pin.annotation = annotation
        return pin
    }
}
