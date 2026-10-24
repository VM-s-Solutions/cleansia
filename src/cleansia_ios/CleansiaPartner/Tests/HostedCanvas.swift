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

/// A view in a window of the given size whose safe area is exactly the one given: the system's own is dropped and
/// the given one laid in, so every simulator reads the same insets.
@MainActor
final class HostedCanvas {
    let window: UIWindow
    let host: UIViewController

    init(size: CGSize, safeArea: EdgeInsets = EdgeInsets(), @ViewBuilder content: () -> some View) {
        let root = content()
            .safeAreaInset(edge: .top, spacing: 0) { Color.clear.frame(height: safeArea.top) }
            .safeAreaInset(edge: .bottom, spacing: 0) { Color.clear.frame(height: safeArea.bottom) }
        let host = UIHostingController(rootView: root)
        if #available(iOS 16.4, *) {
            host.safeAreaRegions = []
        }
        let window = UIWindow(frame: CGRect(origin: .zero, size: size))
        window.rootViewController = host
        window.makeKeyAndVisible()
        host.view.frame = window.bounds
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
    }

    private func pause() async {
        try? await Task.sleep(nanoseconds: 30_000_000)
        host.view.layoutIfNeeded()
    }
}
