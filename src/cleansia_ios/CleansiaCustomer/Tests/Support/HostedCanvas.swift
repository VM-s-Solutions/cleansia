import SwiftUI
import UIKit
import XCTest

/// The frames hosted views report about themselves, in the window's coordinates.
@MainActor
final class LayoutProbes {
    private(set) var frames: [String: CGRect] = [:]

    func record(_ name: String, frame: CGRect) {
        frames[name] = frame
    }
}

extension View {
    func probed(_ name: String, in probes: LayoutProbes) -> some View {
        background(GeometryReader { proxy in
            let frame = proxy.frame(in: .global)
            Color.clear
                .onAppear { probes.record(name, frame: frame) }
                .onChange(of: frame) { probes.record(name, frame: $0) }
        })
    }
}

/// A view in a window of the given size in the app's own scene, with no safe area of its own, so every simulator lays
/// it out the same. The window that was key before is key again on `close()`.
@MainActor
final class HostedCanvas {
    let window: UIWindow
    let host: UIViewController
    private let previous: UIWindow?

    init(size: CGSize, @ViewBuilder content: () -> some View) throws {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        previous = scenes.flatMap(\.windows).first { $0.isKeyWindow }
        let host = UIHostingController(rootView: content())
        if #available(iOS 16.4, *) {
            host.safeAreaRegions = []
        }
        let window = try UIWindow(windowScene: XCTUnwrap(scenes.first, "the test host has no scene"))
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host
        window.makeKeyAndVisible()
        host.view.layoutIfNeeded()
        self.window = window
        self.host = host
    }

    /// Runs the layout passes and appear callbacks that follow the first pass until `condition` holds, then a
    /// few more, so a value that settles late is read settled.
    func settle(timeout: TimeInterval = 3, until condition: () -> Bool = { true }) async {
        let deadline = Date().addingTimeInterval(timeout)
        while !condition(), Date() < deadline {
            await pause()
        }
        for _ in 0 ..< 3 {
            await pause()
        }
    }

    func close() {
        window.isHidden = true
        window.rootViewController = nil
        previous?.makeKeyAndVisible()
    }

    func views<V: UIView>(of _: V.Type) -> [V] {
        Self.views(under: host.view).compactMap { $0 as? V }
    }

    func frame(of view: UIView) -> CGRect {
        view.convert(view.bounds, to: window)
    }

    private func pause() async {
        try? await Task.sleep(nanoseconds: 30_000_000)
        host.view.layoutIfNeeded()
    }

    private static func views(under view: UIView) -> [UIView] {
        [view] + view.subviews.flatMap { views(under: $0) }
    }
}
