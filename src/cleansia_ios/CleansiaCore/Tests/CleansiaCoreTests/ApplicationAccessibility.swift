import Darwin
import Foundation
import UIKit

/// SwiftUI builds a hosted view's accessibility tree only while the process has accessibility on, and a simulator
/// that has never run VoiceOver or Accessibility Inspector has it off (CI's never has), so a test that reads the
/// tree finds nothing there. This turns it on for the test process through libAccessibility's own switch, the one
/// those tools flip; on a simulator the library sits under the runtime root.
enum ApplicationAccessibility {
    private typealias SetEnabled = @convention(c) (Bool) -> Void

    static func enable() {
        let library = "/usr/lib/libAccessibility.dylib"
        let root = ProcessInfo.processInfo.environment["IPHONE_SIMULATOR_ROOT"] ?? ""
        guard let handle = dlopen(root + library, RTLD_NOW) ?? dlopen(library, RTLD_NOW),
              let symbol = dlsym(handle, "_AXSApplicationAccessibilitySetEnabled")
        else { return }
        unsafeBitCast(symbol, to: SetEnabled.self)(true)
    }

    static func elements(under view: NSObject, depth: Int = 0) -> [NSObject] {
        guard depth < 60 else { return [] }
        if view.isAccessibilityElement { return [view] }
        if let children = view.accessibilityElements as? [NSObject], !children.isEmpty {
            return children.flatMap { elements(under: $0, depth: depth + 1) }
        }
        let count = view.accessibilityElementCount()
        if count != NSNotFound, count > 0 {
            return (0 ..< count).compactMap { view.accessibilityElement(at: $0) as? NSObject }
                .flatMap { elements(under: $0, depth: depth + 1) }
        }
        return ((view as? UIView)?.subviews ?? []).flatMap { elements(under: $0, depth: depth + 1) }
    }
}
